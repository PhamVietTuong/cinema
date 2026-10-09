import { Injectable, OnDestroy, computed, effect, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatDialog } from '@angular/material/dialog';
import { TranslateService } from '@ngx-translate/core';
import { EMPTY } from 'rxjs';
import { catchError, debounceTime, switchMap, tap } from 'rxjs/operators';
import {
  CinemaServiceAgent, PaymentServiceAgent, PriceBreakdownAdjustment, PriceBreakdownLine, SeatLockSessionService, SelectableSeat,
  apiErrorMessage, foodOrderCap, isGroupBlocked, seatGroupOf, seatLabel,
} from 'CinemaLib';
import { ManagerApprovalService } from './manager-approval.service';
import { PosFoodLine, PosTicket, defaultPriceRow, foodItems, foodUnitPrice, seatItems, ticketPrice } from './pos-calc';
import { PriceOverrideDialog } from './price-override.dialog';
import { TheaterContextService } from './theater-context.service';

export type CounterShowtime = CinemaServiceAgent.CounterShowtimeDTO;
export type CounterPriceRow = PaymentServiceAgent.ShowTimePriceDTO;
export type CounterFood = CinemaServiceAgent.FoodAndDrinkDTO;

/**
 * The counter cart: today's showtimes, the seat map with its live locks, tickets with their patron category, food and combos,
 * the attached member, discount / gift-card codes, price overrides and the server Quote that drives every total.
 * Provided by the page or dialog that hosts the counter (POS, exchange) together with `SeatLockSessionService`; the
 * `staff-counter-cart` / `staff-counter-summary` components render it and the host adds its own payment step.
 */
@Injectable()
export class CounterCartService implements OnDestroy {
  private readonly _payment = inject(PaymentServiceAgent.HttpService);
  private readonly _cinema = inject(CinemaServiceAgent.HttpService);
  private readonly _session = inject(SeatLockSessionService);
  private readonly _approval = inject(ManagerApprovalService);
  private readonly _dialog = inject(MatDialog);
  private readonly _translate = inject(TranslateService);
  readonly theaterCtx = inject(TheaterContextService);

  readonly theaterId = computed(() => this.theaterCtx.currentTheaterId() ?? '');

  // ---- showtime and seats
  readonly showtimes = signal<CounterShowtime[]>([]);
  readonly loadingShowtimes = signal(false);
  readonly fnbOnly = signal(false);
  readonly showtime = signal<CounterShowtime | null>(null);
  readonly seats = signal<SelectableSeat[]>([]);
  readonly loadingSeats = signal(false);
  readonly priceRows = signal<CounterPriceRow[]>([]);
  readonly tickets = signal<PosTicket[]>([]);
  readonly seatWarning = signal('');
  private _loadSeq = 0;

  // ---- food
  readonly foods = signal<CounterFood[]>([]);
  readonly foodQty = signal<Record<string, number>>({});
  private readonly _foodOverrides = signal<Record<string, number>>({});
  readonly foodLines = computed<PosFoodLine[]>(() => {
    const qty = this.foodQty();
    const overrides = this._foodOverrides();
    return this.foods()
      .filter(f => (qty[f.id!] ?? 0) > 0)
      .map(f => ({ foodAndDrinkId: f.id!, name: f.name ?? '', unitPrice: f.price ?? 0, quantity: qty[f.id!], overridePrice: overrides[f.id!] }));
  });

  // ---- member, codes, override
  readonly phone = signal('');
  readonly customer = signal<CinemaServiceAgent.CounterCustomerDTO | null>(null);
  readonly customerError = signal('');
  readonly points = signal(0);
  readonly discountCode = signal('');
  readonly giftCardCode = signal('');
  /** Manager approval kept while any price override is in force; absent for an approver. */
  private readonly _override = signal<CinemaServiceAgent.ManagerOverrideDTO | undefined>(undefined);

  // ---- quote
  readonly quote = signal<CinemaServiceAgent.CounterQuoteDTO | null>(null);
  readonly quoting = signal(false);
  readonly quoteError = signal('');

  readonly hasItems = computed(() => this.tickets().length > 0 || this.foodLines().length > 0);
  readonly due = computed(() => this.quote()?.finalAmount ?? 0);

  readonly breakdownLines = computed<PriceBreakdownLine[]>(() =>
    (this.quote()?.lines ?? []).map(l => ({
      label: l.description ?? '',
      quantity: l.quantity,
      amount: l.lineTotal ?? 0,
      listAmount: (l.listUnitPrice ?? l.unitPrice ?? 0) * (l.quantity ?? 1),
    })));
  readonly breakdownAdjustments = computed<PriceBreakdownAdjustment[]>(() => {
    const q = this.quote();
    return [
      { labelKey: 'pos.quote.discount', amount: q?.discountAmount ?? 0 },
      { labelKey: 'pos.quote.points', amount: q?.pointsValue ?? 0 },
      { labelKey: 'pos.quote.giftCard', amount: q?.giftCardAmount ?? 0 },
    ];
  });

  /** The sale request without tenders (null while the cart is empty); the host adds the tenders at sell time. */
  readonly quoteRequest = computed(() => {
    const theaterId = this.theaterId();
    const tickets = this.tickets();
    const foods = this.foodLines();
    if (!theaterId || (tickets.length === 0 && foods.length === 0)) {
      return null;
    }
    const st = this.showtime();
    return {
      theaterId,
      showTimeId: tickets.length > 0 ? st?.showTimeId : undefined,
      roomId: tickets.length > 0 ? st?.roomId : undefined,
      seats: seatItems(tickets),
      foods: foodItems(foods),
      customerUserId: this.customer()?.id,
      discountCode: this.discountCode().trim() || undefined,
      giftCardCode: this.giftCardCode().trim() || undefined,
      pointsToRedeem: this.customer() ? this.points() : 0,
      override: this._override(),
    };
  });

  /** SignalR connection id to send with the sale so the server can release this terminal's own seat locks. */
  get connectionId(): string | undefined {
    return this._session.connectionId ?? undefined;
  }

  constructor() {
    // Another counter's or customer's seat events.
    this._session.locked$.pipe(takeUntilDestroyed()).subscribe(id => this._setLocked(id, true));
    this._session.unlocked$.pipe(takeUntilDestroyed()).subscribe(id => this._setLocked(id, false));
    this._session.lockFailed$.pipe(takeUntilDestroyed()).subscribe(id => this._setLocked(id, true));
    this._session.booked$.pipe(takeUntilDestroyed()).subscribe(ids => this._setBooked(ids));

    // Re-quote whenever the cart, member, codes or points change.
    toObservable(this.quoteRequest).pipe(
      tap(() => this.quoting.set(true)),
      debounceTime(250),
      switchMap(req => {
        if (!req) {
          this.quote.set(null);
          this.quoteError.set('');
          this.quoting.set(false);
          return EMPTY;
        }
        const full = { ...req, tenders: [], connectionId: this._session.connectionId ?? undefined };
        return this._cinema.quote(CinemaServiceAgent.CounterSaleRequest.fromJS(full)).pipe(
          tap(q => {
            this.quote.set(q);
            this.quoteError.set('');
            this.quoting.set(false);
          }),
          catchError(err => {
            this.quote.set(null);
            this.quoteError.set(apiErrorMessage(err, this._translate.instant('pos.quote.failed')));
            this.quoting.set(false);
            return EMPTY;
          }),
        );
      }),
      takeUntilDestroyed(),
    ).subscribe();

    // Reload everything for the theater (an Admin switching it in the topbar included).
    effect(() => {
      const id = this.theaterId();
      untracked(() => this._onTheaterChanged(id));
    });
  }

  ngOnDestroy(): void {
    void this._session.stop();
  }

  // ---- loading

  private _onTheaterChanged(theaterId: string): void {
    this.reset();
    this.showtime.set(null);
    this.showtimes.set([]);
    this.foods.set([]);
    if (!theaterId) {
      return;
    }
    this.loadingShowtimes.set(true);
    this._cinema.getShowtimesToday(CinemaServiceAgent.ShowtimesTodayRequest.fromJS({ theaterId })).subscribe({
      next: list => {
        this.showtimes.set(list ?? []);
        this.loadingShowtimes.set(false);
      },
      error: () => this.loadingShowtimes.set(false),
    });
    this._loadFoods(theaterId);
  }

  private _loadFoods(theaterId: string): void {
    this._cinema.getFoodAndDrinks(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 100, filters: { theaterId } })).subscribe({
      next: r => {
        this.foods.set((r.results ?? []).filter(f => f.isAvailable));
        this._clampFood();
      },
    });
  }

  selectFnbOnly(): void {
    this._leaveShowtime();
    this.fnbOnly.set(true);
    this.showtime.set(null);
  }

  selectShowtime(st: CounterShowtime): void {
    if (st.hasEnded) {
      return;
    }
    this._leaveShowtime();
    this.fnbOnly.set(false);
    this.showtime.set(st);
    const seq = ++this._loadSeq;
    this.loadingSeats.set(true);
    this._payment.getSeats(PaymentServiceAgent.PagingSearchDTO.fromJS({ filters: { showTimeId: st.showTimeId, roomId: st.roomId } })).subscribe({
      next: r => {
        if (seq !== this._loadSeq) {
          return;
        }
        this.seats.set(r.results ?? []);
        this._applyGate();
        this.loadingSeats.set(false);
      },
      error: () => {
        if (seq === this._loadSeq) {
          this.loadingSeats.set(false);
        }
      },
    });
    this._payment.getShowTimePrices(st.showTimeId!, st.roomId!).subscribe({
      next: rows => {
        if (seq === this._loadSeq) {
          this.priceRows.set(rows ?? []);
          this._applyGate();
        }
      },
    });
    void this._session.start(st.showTimeId!, st.roomId!);
  }

  /** Releases the seats held for the showtime being left and drops its selection. */
  private _leaveShowtime(): void {
    this._loadSeq++;
    for (const t of this.tickets()) {
      for (const id of t.seatIds) {
        this._session.unlock(id);
      }
    }
    this.tickets.set([]);
    this.seats.set([]);
    this.priceRows.set([]);
    this.seatWarning.set('');
    this._session.forgetAll();
    void this._session.stop();
    this._clearOverrideIfUnused();
  }

  // ---- seats

  /** Price rows (patron categories) that apply to a seat kind. */
  rowsFor(isDouble: boolean): CounterPriceRow[] {
    return this.priceRows()
      .filter(r => !!r.isDouble === isDouble)
      .sort((a, b) => (a.patronCategoryName ?? '').localeCompare(b.patronCategoryName ?? ''));
  }

  /** `busy` blocks changes while a sale is being submitted. */
  toggleSeat(seat: SelectableSeat, busy = false): void {
    if (busy || this.loadingSeats()) {
      return;
    }
    const existing = this.tickets().find(t => t.seatIds.includes(seat.id!));
    const group = seatGroupOf(this.seats(), seat);
    if (existing) {
      this._releaseTicket(existing, true);
    } else {
      if (isGroupBlocked(group)) {
        return;
      }
      const defaultRow = defaultPriceRow(this.rowsFor(!!seat.isDouble));
      if (!defaultRow) {
        this.seatWarning.set(this._translate.instant('pos.seats.noCategory'));
        return;
      }
      this.seatWarning.set('');
      for (const s of group) {
        s.isSelected = true;
        this._session.lock(s.id!);
      }
      this.tickets.update(list => [...list, {
        seatIds: group.map(s => s.id!),
        label: group.map(s => seatLabel(s)).join('-'),
        isDouble: !!seat.isDouble,
        patronCategoryId: defaultRow.patronCategoryId!,
      }]);
    }
    this._applyGate();
  }

  /** Removes a ticket from the list (deselects its seats). */
  toggleSeatByTicket(ticket: PosTicket): void {
    this._releaseTicket(ticket, true);
    this._applyGate();
  }

  /** Deselects a ticket's seats; `unlock` tells the hub (false when the lock is already gone). */
  private _releaseTicket(ticket: PosTicket, unlock: boolean): void {
    for (const id of ticket.seatIds) {
      const seat = this.seats().find(s => s.id === id);
      if (seat) {
        seat.isSelected = false;
      }
      if (unlock) {
        this._session.unlock(id);
      } else {
        this._session.forget(id);
      }
    }
    this.tickets.update(list => list.filter(t => t !== ticket));
    this._clearOverrideIfUnused();
  }

  setCategory(ticket: PosTicket, patronCategoryId: string): void {
    this.tickets.update(list => list.map(t => (t === ticket ? { ...t, patronCategoryId, overridePrice: undefined } : t)));
    this._clearOverrideIfUnused();
  }

  priceOf(ticket: PosTicket): number {
    return ticketPrice(ticket, this.priceRows());
  }

  /** Recomputes which seats can be clicked: free, and the kind has at least one patron category. */
  private _applyGate(): void {
    for (const seat of this.seats()) {
      const hasCategory = this.priceRows().length === 0 || this.rowsFor(!!seat.isDouble).length > 0;
      seat.isAllowedForPatronCategory = hasCategory;
      seat.isSelectable = seat.status === PaymentServiceAgent.SeatStatus.Available && !seat.isLocked && hasCategory;
    }
    this.seats.update(list => [...list]);
  }

  private _setLocked(seatId: string, locked: boolean): void {
    const seat = this.seats().find(s => s.id === seatId);
    if (!seat) {
      return;
    }
    seat.isLocked = locked;
    const ticket = this.tickets().find(t => t.seatIds.includes(seatId));
    if (locked && ticket) {
      this._releaseTicket(ticket, false);
    }
    this._applyGate();
  }

  private _setBooked(seatIds: string[]): void {
    for (const id of seatIds) {
      const seat = this.seats().find(s => s.id === id);
      if (!seat) {
        continue;
      }
      seat.status = PaymentServiceAgent.SeatStatus.Occupied;
      seat.isLocked = false;
      seat.isSelected = false;
      const ticket = this.tickets().find(t => t.seatIds.includes(id));
      if (ticket) {
        this._releaseTicket(ticket, false);
      }
    }
    this._applyGate();
  }

  // ---- food

  cap(f: CounterFood): number | null {
    return foodOrderCap(f);
  }

  qtyOf(f: CounterFood): number {
    return this.foodQty()[f.id!] ?? 0;
  }

  canIncFood(f: CounterFood): boolean {
    const cap = this.cap(f);
    return cap === null || this.qtyOf(f) < cap;
  }

  incFood(f: CounterFood): void {
    if (this.canIncFood(f)) {
      this.foodQty.update(q => ({ ...q, [f.id!]: this.qtyOf(f) + 1 }));
    }
  }

  decFood(f: CounterFood): void {
    const next = Math.max(0, this.qtyOf(f) - 1);
    this.foodQty.update(q => ({ ...q, [f.id!]: next }));
    if (next === 0) {
      this._foodOverrides.update(o => {
        const { [f.id!]: _removed, ...rest } = o;
        return rest;
      });
      this._clearOverrideIfUnused();
    }
  }

  foodPrice(line: PosFoodLine): number {
    return foodUnitPrice(line);
  }

  private _clampFood(): void {
    this.foodQty.update(q => {
      const next = { ...q };
      for (const f of this.foods()) {
        const cap = this.cap(f);
        if (cap !== null && (next[f.id!] ?? 0) > cap) {
          next[f.id!] = cap;
        }
      }
      return next;
    });
  }

  // ---- price override

  overrideTicket(ticket: PosTicket): void {
    this._askPrice(ticket.label, this.priceOf(ticket), ticket.overridePrice !== undefined, price => {
      this.tickets.update(list => list.map(t => (t === ticket ? { ...t, overridePrice: price ?? undefined } : t)));
    });
  }

  overrideFood(line: PosFoodLine): void {
    this._askPrice(line.name, foodUnitPrice(line), line.overridePrice !== undefined, price => {
      this._foodOverrides.update(o => {
        const next = { ...o };
        if (price === null) {
          delete next[line.foodAndDrinkId];
        } else {
          next[line.foodAndDrinkId] = price;
        }
        return next;
      });
    });
  }

  /** Asks for the new price, then (unless restoring the list price) for manager approval, then applies it. */
  private _askPrice(label: string, current: number, hasOverride: boolean, apply: (price: number | null) => void): void {
    this._dialog.open(PriceOverrideDialog, { width: '420px', maxWidth: '95vw', data: { label, current, hasOverride } })
      .afterClosed().subscribe(price => {
        if (price === undefined) {
          return;
        }
        if (price === null) {
          apply(null);
          this._clearOverrideIfUnused();
          return;
        }
        this._approval.request().subscribe(approval => {
          if (!approval) {
            return;
          }
          if (approval.override) {
            this._override.set(approval.override);
          }
          apply(price);
        });
      });
  }

  /** Drops the stored manager approval once no line carries an override any more. */
  private _clearOverrideIfUnused(): void {
    const any = this.tickets().some(t => t.overridePrice !== undefined) || Object.keys(this._foodOverrides()).length > 0;
    if (!any) {
      this._override.set(undefined);
    }
  }

  // ---- member and codes

  findMember(): void {
    const phone = this.phone().trim();
    if (!phone) {
      return;
    }
    this.customerError.set('');
    this._cinema.findCustomer(CinemaServiceAgent.FindCustomerRequest.fromJS({ phone })).subscribe({
      next: c => {
        this.customer.set(c);
        this.points.set(0);
      },
      error: err => {
        this.customer.set(null);
        const notFound = (err as { status?: number })?.status === 404;
        this.customerError.set(notFound ? this._translate.instant('pos.member.notFound') : apiErrorMessage(err, this._translate.instant('pos.member.failed')));
      },
    });
  }

  detachMember(): void {
    this.customer.set(null);
    this.points.set(0);
    this.phone.set('');
    this.customerError.set('');
  }

  setPoints(value: number | null): void {
    const max = this.customer()?.points ?? 0;
    this.points.set(Math.max(0, Math.min(Math.floor(value ?? 0), max)));
  }

  // ---- after a sale

  /** Marks the sold tickets' seats occupied, empties the cart (keeping the showtime) and reloads the food stock. */
  afterSale(sold: PosTicket[]): void {
    for (const t of sold) {
      for (const id of t.seatIds) {
        const seat = this.seats().find(s => s.id === id);
        if (seat) {
          seat.status = PaymentServiceAgent.SeatStatus.Occupied;
          seat.isSelected = false;
          seat.isLocked = false;
        }
        this._session.forget(id);
      }
    }
    this.reset(true);
    this._applyGate();
    this._loadFoods(this.theaterId());
  }

  /** Empties the cart; `keepSeats` leaves the showtime and its seat map as they are. */
  reset(keepSeats = false): void {
    if (!keepSeats) {
      this._leaveShowtime();
    }
    this.tickets.set([]);
    this.foodQty.set({});
    this._foodOverrides.set({});
    this._override.set(undefined);
    this.detachMember();
    this.discountCode.set('');
    this.giftCardCode.set('');
    this.quote.set(null);
    this.quoteError.set('');
  }
}

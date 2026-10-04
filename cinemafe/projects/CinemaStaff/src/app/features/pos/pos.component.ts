import { Component, OnDestroy, computed, effect, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatDialog } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { TranslateService } from '@ngx-translate/core';
import { EMPTY } from 'rxjs';
import { catchError, debounceTime, switchMap, tap } from 'rxjs/operators';
import {
  CinemaServiceAgent, CounterTenderValues, EmptyStateComponent, PaymentServiceAgent, PriceBreakdownAdjustment, PriceBreakdownComponent,
  PriceBreakdownLine, SeatLockSessionService, SeatMapComponent, SelectableSeat, SharedModule, StaffServiceAgent, ToastService,
  apiErrorMessage, foodOrderCap, isGroupBlocked, seatGroupOf, seatLabel,
} from 'CinemaLib';
import { CashDrawerService } from '../../core/cash-drawer.service';
import { ManagerApprovalService } from '../../core/manager-approval.service';
import { TheaterContextService } from '../../core/theater-context.service';
import { PosFoodLine, PosTicket, TenderEntry, foodItems, foodUnitPrice, remainingAfter, seatItems, settle, ticketPrice } from './pos-calc';
import { PosReceiptComponent, SaleReceipt } from './pos-receipt.component';
import { PriceOverrideDialog } from './price-override.dialog';

type Showtime = StaffServiceAgent.CounterShowtimeDTO;
type PriceRow = PaymentServiceAgent.ShowTimePriceDTO;
type Food = CinemaServiceAgent.FoodAndDrinkDTO;

/**
 * Counter point of sale: pick today's showtime (or sell food only), pick seats and a patron category per seat,
 * add food and combos, optionally attach a member, then take cash / card / QR tenders and print the tickets.
 * The server's Quote drives every total shown; selling goes through BoxOffice.Sell.
 */
@Component({
  selector: 'staff-pos',
  standalone: true,
  imports: [SharedModule, MatProgressBarModule, SeatMapComponent, PriceBreakdownComponent, PosReceiptComponent, EmptyStateComponent],
  providers: [SeatLockSessionService],
  templateUrl: './pos.component.html',
  styleUrl: './pos.component.scss',
})
export class PosComponent implements OnDestroy {
  private readonly _box = inject(StaffServiceAgent.BoxOfficeHttpService);
  private readonly _payment = inject(PaymentServiceAgent.HttpService);
  private readonly _cinema = inject(CinemaServiceAgent.HttpService);
  private readonly _session = inject(SeatLockSessionService);
  private readonly _approval = inject(ManagerApprovalService);
  private readonly _dialog = inject(MatDialog);
  private readonly _translate = inject(TranslateService);
  private readonly _toast = inject(ToastService);
  readonly theaterCtx = inject(TheaterContextService);
  readonly drawer = inject(CashDrawerService);

  readonly tenderOptions = CounterTenderValues;
  readonly isCash = (m: StaffServiceAgent.PaymentTender): boolean => m === StaffServiceAgent.PaymentTender.Cash;

  readonly theaterId = computed(() => this.theaterCtx.currentTheaterId() ?? '');

  // ---- showtime and seats
  readonly showtimes = signal<Showtime[]>([]);
  readonly loadingShowtimes = signal(false);
  readonly fnbOnly = signal(false);
  readonly showtime = signal<Showtime | null>(null);
  readonly seats = signal<SelectableSeat[]>([]);
  readonly loadingSeats = signal(false);
  readonly priceRows = signal<PriceRow[]>([]);
  readonly tickets = signal<PosTicket[]>([]);
  readonly seatWarning = signal('');
  private _loadSeq = 0;

  // ---- food
  readonly foods = signal<Food[]>([]);
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
  readonly customer = signal<StaffServiceAgent.CounterCustomerDTO | null>(null);
  readonly customerError = signal('');
  readonly points = signal(0);
  readonly discountCode = signal('');
  readonly giftCardCode = signal('');
  /** Manager approval kept while any price override is in force; absent for an approver. */
  private readonly _override = signal<StaffServiceAgent.ManagerOverrideDTO | undefined>(undefined);

  // ---- quote and payment
  readonly quote = signal<StaffServiceAgent.CounterQuoteDTO | null>(null);
  readonly quoting = signal(false);
  readonly quoteError = signal('');
  readonly tenders = signal<TenderEntry[]>([]);
  readonly selling = signal(false);
  readonly receipt = signal<SaleReceipt | null>(null);

  readonly hasItems = computed(() => this.tickets().length > 0 || this.foodLines().length > 0);
  readonly due = computed(() => this.quote()?.finalAmount ?? 0);
  readonly settlement = computed(() => settle(this.due(), this.tenders()));
  readonly needsDrawer = computed(() => this.settlement().usesCash && !this.drawer.isOpen());
  readonly canSell = computed(() =>
    !!this.quote() && !this.quoting() && !this.selling() && this.hasItems() && this.settlement().complete && !this.needsDrawer());

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

  private readonly _quoteRequest = computed(() => {
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

  constructor() {
    // Another counter's or customer's seat events.
    this._session.locked$.pipe(takeUntilDestroyed()).subscribe(id => this._setLocked(id, true));
    this._session.unlocked$.pipe(takeUntilDestroyed()).subscribe(id => this._setLocked(id, false));
    this._session.lockFailed$.pipe(takeUntilDestroyed()).subscribe(id => this._setLocked(id, true));
    this._session.booked$.pipe(takeUntilDestroyed()).subscribe(ids => this._setBooked(ids));

    // Re-quote whenever the cart, member, codes or points change.
    toObservable(this._quoteRequest).pipe(
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
        return this._box.quote(StaffServiceAgent.CounterSaleRequest.fromJS(full)).pipe(
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
    this._resetCart();
    this.showtime.set(null);
    this.showtimes.set([]);
    this.foods.set([]);
    this.receipt.set(null);
    if (!theaterId) {
      return;
    }
    this.loadingShowtimes.set(true);
    this._box.getShowtimesToday(StaffServiceAgent.ShowtimesTodayRequest.fromJS({ theaterId })).subscribe({
      next: list => {
        this.showtimes.set(list ?? []);
        this.loadingShowtimes.set(false);
      },
      error: () => this.loadingShowtimes.set(false),
    });
    this._loadFoods(theaterId);
    this.drawer.refresh().subscribe({ error: () => {} });
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

  selectShowtime(st: Showtime): void {
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
  rowsFor(isDouble: boolean): PriceRow[] {
    return this.priceRows()
      .filter(r => !!r.isDouble === isDouble)
      .sort((a, b) => (a.patronCategoryName ?? '').localeCompare(b.patronCategoryName ?? ''));
  }

  toggleSeat(seat: SelectableSeat): void {
    if (this.selling() || this.loadingSeats()) {
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
      const defaultRow = this.rowsFor(!!seat.isDouble)[0];
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

  cap(f: Food): number | null {
    return foodOrderCap(f);
  }

  qtyOf(f: Food): number {
    return this.foodQty()[f.id!] ?? 0;
  }

  canIncFood(f: Food): boolean {
    const cap = this.cap(f);
    return cap === null || this.qtyOf(f) < cap;
  }

  incFood(f: Food): void {
    if (this.canIncFood(f)) {
      this.foodQty.update(q => ({ ...q, [f.id!]: this.qtyOf(f) + 1 }));
    }
  }

  decFood(f: Food): void {
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
    this._box.findCustomer(StaffServiceAgent.FindCustomerRequest.fromJS({ phone })).subscribe({
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

  // ---- tenders

  addTender(method: StaffServiceAgent.PaymentTender): void {
    const entries = this.tenders();
    this.tenders.set([...entries, { method, amount: remainingAfter(this.due(), entries, -1), reference: '' }]);
  }

  updateTender(index: number, patch: Partial<TenderEntry>): void {
    this.tenders.update(list => list.map((t, i) => (i === index ? { ...t, ...patch } : t)));
  }

  fillRemaining(index: number): void {
    this.updateTender(index, { amount: remainingAfter(this.due(), this.tenders(), index) });
  }

  removeTender(index: number): void {
    this.tenders.update(list => list.filter((_, i) => i !== index));
  }

  // ---- sale

  sell(): void {
    const req = this._quoteRequest();
    if (!req || !this.canSell()) {
      return;
    }
    const st = this.showtime();
    const tickets = this.tickets();
    const full = {
      ...req,
      connectionId: this._session.connectionId ?? undefined,
      tenders: this.tenders().map(t => ({
        method: t.method,
        amount: Math.round(t.amount),
        reference: this.isCash(t.method) ? undefined : t.reference.trim(),
      })),
    };
    this.selling.set(true);
    this._box.sell(StaffServiceAgent.CounterSaleRequest.fromJS(full)).subscribe({
      next: result => {
        this.selling.set(false);
        this.receipt.set({
          result,
          movieTitle: tickets.length > 0 ? (st?.movieTitle ?? '') : '',
          roomName: tickets.length > 0 ? (st?.roomName ?? '') : '',
          showTime: tickets.length > 0 ? st?.startTime : undefined,
          theaterName: this.theaterCtx.currentTheaterName() ?? '',
        });
        this._toast.success(this._translate.instant('pos.sell.success', { code: result.invoiceCode }));
        this._afterSale(tickets);
      },
      error: err => {
        this.selling.set(false);
        this._toast.error(apiErrorMessage(err, this._translate.instant('pos.sell.failed')));
      },
    });
  }

  private _afterSale(sold: PosTicket[]): void {
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
    this._resetCart(true);
    this._applyGate();
    this._loadFoods(this.theaterId());
    this.drawer.refresh().subscribe({ error: () => {} });
  }

  newSale(): void {
    this.receipt.set(null);
  }

  private _resetCart(keepSeats = false): void {
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
    this.tenders.set([]);
    this.quote.set(null);
    this.quoteError.set('');
  }
}

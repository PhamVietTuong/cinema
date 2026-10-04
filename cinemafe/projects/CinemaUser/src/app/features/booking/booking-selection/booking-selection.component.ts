import { ChangeDetectorRef, Component, EventEmitter, Input, OnChanges, OnDestroy, OnInit, Output } from '@angular/core';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { Subscription, take } from 'rxjs';
import {
  SharedModule, PaymentServiceAgent, CinemaServiceAgent, BookingHubService, seatKindLabel, selectIsAuthenticated,
  SeatMapComponent, SeatLockSessionService, SelectableSeat, seatRows, seatsByRow, seatLabel, seatGroupOf, isGroupBlocked, physicalSeatPrice, foodOrderCap,
} from 'CinemaLib';
import { TranslateService } from '@ngx-translate/core';
import { BookingCheckoutState, stashPendingCheckout } from '../booking-checkout/booking-checkout.state';

type ShowTimePriceDTO = PaymentServiceAgent.ShowTimePriceDTO;

/** One ticket the customer is buying: a specific (patron category, seat kind) row — each row already
 * has its own PatronCategory id, so a slot's kind is fixed at creation time, never resolved later.
 * A Double seat is one bookable unit priced once (quantity 1 = one couple seat), but is still two
 * physical Seat rows sharing a group id — `seatIds` holds both once filled, one for a Standard slot. */
interface TicketSlot {
  patronCategoryId: string;
  isDouble: boolean;
  seatIds: string[];
}

/**
 * The ticket-quantity + seat-map + snacks booking UI, shared by the routed `/booking/seats` page
 * (BookingPageComponent, a thin wrapper) and the inline panel embedded on the movie-detail page.
 * Targets whichever `showTimeId`/`roomId` it's given; changing them mid-life (the inline-panel case)
 * releases the previous showtime's seat locks and reloads everything for the new one. Payment lives
 * on a separate page (`/booking/checkout`) reached via `proceedToCheckout()`.
 */
@Component({
  selector: 'app-booking-selection',
  standalone: true,
  imports: [SharedModule, SeatMapComponent],
  templateUrl: './booking-selection.component.html',
  styleUrl: './booking-selection.component.scss'
})
export class BookingSelectionComponent implements OnInit, OnChanges, OnDestroy {
  static readonly MAX_TICKETS = 10;

  @Input({ required: true }) showTimeId = '';
  @Input({ required: true }) roomId = '';
  /** True when rendered inline on the movie-detail page (adds a header + close control) rather than
   * as the full routed page (which supplies its own page header). */
  @Input() embedded = false;
  /** "Cinema · 19:30 · IMAX 2D" — composed by the caller, shown in the embedded header. */
  @Input() headerLabel = '';
  /** Emitted when the embedded panel's close/cancel control is used, after locks are released. */
  @Output() closed = new EventEmitter<void>();

  seats: SelectableSeat[] = [];
  selectedSeats: SelectableSeat[] = [];
  loadingSeats = true;
  SeatStatus = PaymentServiceAgent.SeatStatus;
  readonly seatKindLabel = seatKindLabel;
  /** Name of the room being booked, shown in the order bar when no embedded header label is supplied. */
  roomName = '';
  private _theaterId = '';
  /** Which showTimeId/roomId the currently-loaded seats/locks belong to — distinct from the
   * @Input values, which Angular has already updated to the NEW target by the time a switch away
   * from this one needs to unlock its seats. */
  private _loadedShowTimeId = '';
  private _loadedRoomId = '';
  /** `${showTimeId}:${roomId}` last (re)loaded, to tell a genuine target change from a spurious
   * change-detection pass with the same inputs. */
  private _currentKey = '';
  /** Bumped on every (re)load; an in-flight request whose sequence no longer matches on arrival
   * belongs to a showtime we've since switched away from and must not overwrite current state. */
  private _loadSeq = 0;
  /** Chains switch/cancel operations so a rapid double-click can't interleave two teardowns. */
  private _switchChain: Promise<void> = Promise.resolve();

  /** Countdown to the soonest-expiring real-time seat lock among the seats currently selected
   * (each seat is locked, and its 5-minute clock starts, independently at click time). Purely
   * informational — the server is the actual authority on lock expiry (IsSeatLocked). */
  holdActive = false;
  holdSecondsLeft = 0;
  holdExpired = false;
  private _holdTimer: any;
  private _lockSession?: SeatLockSessionService;

  /** The resolved price list for this showtime+room: one row per (patron category, seat kind)
   * combination actually bookable here (all pricing factors already applied server-side). Each row
   * already carries its own PatronCategory id (Standard and Double are separate PatronCategory rows),
   * so each is shown as its own independent ticket-type card — never merged. Quantities build
   * `slots` — one entry per ticket, in category-declared order — each filled by exactly one seat
   * click. */
  showTimePrices: ShowTimePriceDTO[] = [];
  ticketQty: Record<string, number> = {};
  slots: TicketSlot[] = [];
  categoryWarning = '';

  /** Ticket-type cards, grouped by category name then Standard-before-Double, purely for a stable,
   * readable display order — each row is still its own independent card/quantity. */
  get priceRows(): ShowTimePriceDTO[] {
    return [...this.showTimePrices].sort((a, b) =>
      (a.patronCategoryName ?? '').localeCompare(b.patronCategoryName ?? '') || (a.isDouble ? 1 : 0) - (b.isDouble ? 1 : 0));
  }

  canIncrement(row: ShowTimePriceDTO): boolean {
    return this.totalTickets < this.maxTickets;
  }

  private _rowFor(patronCategoryId: string): ShowTimePriceDTO | undefined {
    return this.showTimePrices.find(r => r.patronCategoryId === patronCategoryId);
  }

  foods: CinemaServiceAgent.FoodAndDrinkDTO[] = [];
  foodQty: Record<string, number> = {};
  /** Ids of foods whose imageUrl failed to load — stops re-rendering a broken <img> and shows the fallback instead. */
  private _foodImgFailed = new Set<string>();

  private _subs = new Subscription();
  private _navigatingToCheckout = false;

  constructor(
    private _router: Router,
    private _store: Store,
    private _paymentService: PaymentServiceAgent.HttpService,
    private _cinemaService: CinemaServiceAgent.HttpService,
    private _hub: BookingHubService,
    private _cdr: ChangeDetectorRef,
    private _translate: TranslateService,
  ) {}

  /** The seat-lock session on the booking hub (hold clocks, lock/unlock, room events). Created on
   * first use so it exists before the first load, and torn down in ngOnDestroy. */
  private get _session(): SeatLockSessionService {
    if (!this._lockSession) {
      this._lockSession = new SeatLockSessionService(this._hub);
    }
    return this._lockSession;
  }

  get rows(): string[] {
    return seatRows(this.seats);
  }

  getSeatsByRow(row: string): SelectableSeat[] {
    return seatsByRow(this.seats, row);
  }

  get maxTickets(): number {
    return BookingSelectionComponent.MAX_TICKETS;
  }

  get totalTickets(): number {
    return this.slots.length;
  }

  get remainingTickets(): number {
    return this.slots.filter(s => s.seatIds.length === 0).length;
  }

  get canProceed(): boolean {
    return this.totalTickets > 0 && this.remainingTickets === 0;
  }

/** The (patron category × seat kind) row pricing the given seat, once a slot has claimed it. */
  categoryForSeat(seatId: string): ShowTimePriceDTO | undefined {
    const slot = this.slots.find(sl => sl.seatIds.includes(seatId));
    if (!slot) {
      return undefined;
    }
    return this._rowFor(slot.patronCategoryId);
  }

  /** The server-resolved final price for this seat once a category has claimed it. A Double seat's
   * row price is for the whole couple seat, split across its two physical halves so they sum to the
   * single price the customer was quoted for one seat. Falls back to the seat's own "from" price
   * when nothing has claimed it yet (shouldn't normally be displayed). */
  seatPrice(seat: SelectableSeat): number {
    const price = this.categoryForSeat(seat.id!)?.price ?? seat.price ?? 0;
    return physicalSeatPrice(price, seat.isDouble);
  }

  get totalPrice(): number {
    return this.selectedSeats.reduce((sum, s) => sum + this.seatPrice(s), 0);
  }

  /** One row per purchased ticket (not per physical seat) for the sidebar: a Double ticket combines
   * its two seats into a single line at the whole-seat price, avoiding a misleading "half price"
   * display split across two lines. */
  get selectedTickets(): { label: string; isDouble: boolean; categoryName: string; price: number }[] {
    return this.slots
      .filter(sl => sl.seatIds.length > 0)
      .map(sl => {
        const seats = sl.seatIds
          .map(id => this.seats.find(s => s.id === id))
          .filter((s): s is SelectableSeat => !!s);
        const label = seats.map(s => seatLabel(s)).join('-');
        const row = this._rowFor(sl.patronCategoryId);
        return { label, isDouble: sl.isDouble, categoryName: row?.patronCategoryName ?? '', price: row?.price ?? 0 };
      });
  }

  get foodTotal(): number {
    return this.foods.reduce((sum, f) => sum + (f.price ?? 0) * (this.foodQty[f.id!] ?? 0), 0);
  }

  get selectedFoods(): CinemaServiceAgent.FoodAndDrinkDTO[] {
    return this.foods.filter(f => (this.foodQty[f.id!] ?? 0) > 0);
  }

  get grandTotal(): number {
    return this.totalPrice + this.foodTotal;
  }

  /** The hold countdown as MM:SS. */
  get holdCountdown(): string {
    const m = Math.floor(this.holdSecondsLeft / 60);
    const s = this.holdSecondsLeft % 60;
    return `${m}:${s.toString().padStart(2, '0')}`;
  }

  ngOnInit(): void {
    // Long-lived, root-provided subjects — subscribed once regardless of how many showtimes this
    // instance goes on to target; re-subscribing per switch would fire each handler N times over.
    // (The session already drops our own lock broadcasts.)
    this._subs.add(this._session.locked$.subscribe(seatId => this._setLocked(seatId, true)));
    this._subs.add(this._session.unlocked$.subscribe(seatId => this._setLocked(seatId, false)));
    // Our lock attempt lost the race — revert the optimistic selection.
    this._subs.add(this._session.lockFailed$.subscribe(seatId => this._setLocked(seatId, true)));
    // Someone completed a booking — those seats are now permanently unavailable.
    this._subs.add(this._session.booked$.subscribe(seatIds => this._setBooked(seatIds)));
  }

  ngOnChanges(): void {
    if (!this.showTimeId || !this.roomId) {
      return;
    }
    const key = `${this.showTimeId}:${this.roomId}`;
    if (key === this._currentKey) {
      return;
    }
    const isFirst = this._currentKey === '';
    this._currentKey = key;
    if (isFirst) {
      this._load();
    } else {
      this._switchChain = this._switchChain.then(() => this._doSwitch());
    }
  }

  ngOnDestroy(): void {
    this._subs.unsubscribe();
    if (this._holdTimer) {
      clearInterval(this._holdTimer);
    }
    // Stopping the hub connection releases every seat lock it holds (BookingHub.OnDisconnectedAsync).
    // When moving on to checkout the locks must survive, so only stop here when we are NOT headed
    // there (e.g. the customer navigated away entirely).
    if (!this._navigatingToCheckout) {
      this._session.stop();
    }
    this._session.ngOnDestroy();
  }

  /** Releases the previous showtime's held seats and connection, resets all per-showtime state,
   * then loads the new target (`this.showTimeId`/`this.roomId`, already updated by Angular). */
  private async _doSwitch(): Promise<void> {
    // Bump FIRST: any response still in flight for the showtime being left immediately fails its
    // `seq !== this._loadSeq` check, so it can never repopulate state after we start tearing down
    // (there's a real awaited turn below — stopConnection — during which such a response could
    // otherwise land and be mistaken for current).
    this._loadSeq++;
    for (const s of this.selectedSeats) {
      this._session.unlock(s.id!); // still targets the showtime being left
    }
    this._resetSelectionState();
    // stop() also cancels a same-target start() still mid-handshake (SignalR rejects the pending
    // .start()), which the session's own catch swallows — relied on here rather than tracked
    // explicitly, since BookingHubService exposes no "wait for pending start".
    await this._session.stop();
    this._load();
  }

  /** Collapses the embedded panel: releases held seats, tears down the connection, resets state,
   * and notifies the host so it can hide this component. */
  cancel(): void {
    this._switchChain = this._switchChain.then(async () => {
      // Bump first — same reasoning as _doSwitch, and it also guards against closing WHILE a switch
      // this call is queued behind already kicked off a _load() for a target we're now abandoning.
      this._loadSeq++;
      for (const s of this.selectedSeats) {
        this._session.unlock(s.id!);
      }
      await this._session.stop();
      this._resetSelectionState();
      this._currentKey = '';
      this.closed.emit();
    });
  }

  private _resetSelectionState(): void {
    this.seats = [];
    this.selectedSeats = [];
    this.slots = [];
    this.ticketQty = {};
    this.showTimePrices = [];
    this.foods = [];
    this.foodQty = {};
    this._foodImgFailed.clear();
    this.categoryWarning = '';
    this._session.forgetAll();
    this._theaterId = '';
    this.roomName = '';
    this.loadingSeats = true;
    if (this._holdTimer) {
      clearInterval(this._holdTimer);
      this._holdTimer = null;
    }
    this.holdActive = false;
    this.holdSecondsLeft = 0;
    this.holdExpired = false;
    this._cdr.markForCheck();
  }

  private _load(): void {
    this._loadedShowTimeId = this.showTimeId;
    this._loadedRoomId = this.roomId;
    const seq = ++this._loadSeq;
    this.loadingSeats = true;

    this._paymentService.getSeats(
      PaymentServiceAgent.PagingSearchDTO.fromJS({ filters: { showTimeId: this.showTimeId, roomId: this.roomId } })
    ).subscribe({
      next: r => {
        if (seq !== this._loadSeq) { return; } // superseded by a later switch
        this.seats = r.results ?? [];
        this.loadingSeats = false;
        this._applyGate();
        this._cdr.markForCheck();
      },
      error: () => {
        if (seq !== this._loadSeq) { return; }
        this.loadingSeats = false;
        this._cdr.markForCheck();
      }
    });

    if (this.roomId) {
      this._cinemaService.getRoom(this.roomId).subscribe({
        next: room => {
          if (seq !== this._loadSeq) { return; }
          this.roomName = room?.name ?? '';
          this._cdr.markForCheck();
          if (!room?.theaterId) { return; }
          this._theaterId = room.theaterId;
          this._cinemaService.getFoodAndDrinks(CinemaServiceAgent.PagingSearchDTO.fromJS(
            { pageIndex: 1, pageSize: 100, filters: { theaterId: room.theaterId } }))
            .subscribe(r => {
              if (seq !== this._loadSeq) { return; }
              this.foods = (r.results ?? []).filter(f => f.isAvailable);
              this._clampFoodQty();
              this._cdr.markForCheck();
            });
        },
        error: () => {
          if (seq !== this._loadSeq) { return; }
          this._cdr.markForCheck();
        },
      });
    }

    this._paymentService.getShowTimePrices(this.showTimeId, this.roomId).subscribe({
      next: rows => {
        if (seq !== this._loadSeq) { return; }
        this.showTimePrices = rows ?? [];
        for (const row of this.showTimePrices) {
          this.ticketQty[row.patronCategoryId!] = 0;
        }
        this._cdr.markForCheck();
      },
      error: () => {
        if (seq !== this._loadSeq) { return; }
        this._cdr.markForCheck();
      },
    });

    this._session.start(this.showTimeId, this.roomId); // a failed handshake degrades to non-realtime
  }

  incTicket(row: ShowTimePriceDTO): void {
    this.setTicketQty(row.patronCategoryId!, (this.ticketQty[row.patronCategoryId!] ?? 0) + 1, !!row.isDouble);
  }

  decTicket(row: ShowTimePriceDTO): void {
    this.setTicketQty(row.patronCategoryId!, Math.max(0, (this.ticketQty[row.patronCategoryId!] ?? 0) - 1), !!row.isDouble);
  }

  setTicketQty(patronCategoryId: string, qty: number, isDouble: boolean): void {
    const prev = this.ticketQty[patronCategoryId] ?? 0;
    const otherTotal = this.slots.length - prev;
    const clamped = Math.max(0, Math.min(qty, BookingSelectionComponent.MAX_TICKETS - otherTotal));
    if (clamped === prev) {
      return;
    }
    this.ticketQty[patronCategoryId] = clamped;

    if (clamped > prev) {
      for (let i = prev; i < clamped; i++) {
        this.slots.push({ patronCategoryId, isDouble, seatIds: [] });
      }
      this.categoryWarning = '';
    } else {
      const diff = prev - clamped;
      const forThisCategory = this.slots
        .map((slot, index) => ({ slot, index }))
        .filter(x => x.slot.patronCategoryId === patronCategoryId);
      // Free (unassigned) slots go first; assigned slots are freed last-declared-first.
      const removable = [
        ...forThisCategory.filter(x => x.slot.seatIds.length === 0).map(x => x.index),
        ...forThisCategory.filter(x => x.slot.seatIds.length > 0).map(x => x.index).reverse(),
      ].slice(0, diff);

      // A slot already holds every physical seat of its group (both halves of a double, filled
      // together by toggleSeat), so releasing it needs no separate partner lookup.
      const removedLabels: string[] = [];
      for (const index of removable) {
        const slot = this.slots[index];
        for (const seatId of slot.seatIds) {
          const seat = this.seats.find(s => s.id === seatId);
          if (!seat) {
            continue;
          }
          seat.isSelected = false;
          this.selectedSeats = this.selectedSeats.filter(x => x.id !== seatId);
          this._session.unlock(seatId);
          removedLabels.push(seatLabel(seat));
        }
      }
      for (const index of [...removable].sort((a, b) => b - a)) {
        this.slots.splice(index, 1);
      }

      this.categoryWarning = removedLabels.length
        ? this._translate.instant('booking.tickets.removedByQuantityChange', { seats: removedLabels.join(', ') })
        : '';
    }

    this._applyGate();
    this._refreshHoldTimer();
  }

  /** Picks the first free slot of this seat kind. Returns -1 when no free slot has this kind (either
   * no ticket of this kind was chosen at all, or they're all already filled). */
  private _bestSlotFor(isDouble: boolean): number {
    return this.slots.findIndex(slot => slot.seatIds.length === 0 && slot.isDouble === isDouble);
  }

  /** Recomputes each seat's isAllowedForPatronCategory (is there a chosen ticket of this seat's kind
   * at all) and isSelectable (is there currently a FREE slot for it). */
  private _applyGate(): void {
    if (this.seats.length === 0) {
      return;
    }
    for (const seat of this.seats) {
      const isDouble = !!seat.isDouble;
      const allowedByAny = this.slots.length === 0 || this.slots.some(sl => sl.isDouble === isDouble);
      const hasFreeMatch = this.slots.some(sl => sl.seatIds.length === 0 && sl.isDouble === isDouble);
      seat.isAllowedForPatronCategory = allowedByAny;
      seat.isSelectable = seat.status === PaymentServiceAgent.SeatStatus.Available && !seat.isLocked && allowedByAny && (!!seat.isSelected || hasFreeMatch);
    }
    this._cdr.markForCheck();
  }

  toggleSeat(seat: SelectableSeat): void {
    if (!seat.isSelected && seat.isSelectable === false) {
      return;
    }
    // Double seats are two linked seats sharing a group id — select/lock them together, as one
    // ticket (one slot), since a couple seat is priced and counted as a single bookable unit.
    const group = seatGroupOf(this.seats, seat);
    if (isGroupBlocked(group)) {
      return;
    }

    const select = !seat.isSelected;
    if (select) {
      const slotIndex = this._bestSlotFor(!!seat.isDouble);
      if (slotIndex === -1) {
        this.categoryWarning = this._translate.instant('booking.tickets.capReached');
        this._cdr.markForCheck();
        return;
      }
      this.slots[slotIndex].seatIds = group.map(s => s.id!);
      for (const s of group) {
        s.isSelected = true;
        if (!this.selectedSeats.includes(s)) {
          this.selectedSeats.push(s);
        }
        this._session.lock(s.id!); // a lost race surfaces through lockFailed$
      }
      this.categoryWarning = '';
    } else {
      const slot = this.slots.find(sl => sl.seatIds.includes(seat.id!));
      if (slot) {
        slot.seatIds = [];
      }
      for (const s of group) {
        s.isSelected = false;
        this.selectedSeats = this.selectedSeats.filter(x => x.id !== s.id);
        this._session.unlock(s.id!);
      }
    }

    this._applyGate();
    this._refreshHoldTimer();
  }

  /** The soonest lock-expiry timestamp (ms epoch) among currently selected seats, or null if none
   * are selected. That seat's lock is the first to lapse, so it's the one worth counting down to. */
  private _soonestLockExpiry(): number | null {
    return this._session.soonestExpiry(this.selectedSeats.map(s => s.id!));
  }

  /** Starts/stops the countdown interval as selections come and go, and refreshes it immediately. */
  private _refreshHoldTimer(): void {
    const expiry = this._soonestLockExpiry();
    if (expiry === null) {
      if (this._holdTimer) {
        clearInterval(this._holdTimer);
        this._holdTimer = null;
      }
      this.holdActive = false;
      this.holdExpired = false;
      return;
    }
    this.holdActive = true;
    if (!this._holdTimer) {
      this._holdTimer = setInterval(() => this._refreshHoldTimer(), 1000);
    }
    const remainingMs = expiry - Date.now();
    this.holdSecondsLeft = Math.max(0, Math.ceil(remainingMs / 1000));
    this.holdExpired = remainingMs <= 0;
    // The countdown text is done changing once expired — stop ticking (the banner itself stays
    // shown via holdActive/holdExpired) instead of firing markForCheck every second forever.
    if (this.holdExpired && this._holdTimer) {
      clearInterval(this._holdTimer);
      this._holdTimer = null;
    }
    this._cdr.markForCheck();
  }

  /** Releases the ticket slot holding this seat, if any — frees the WHOLE slot (both halves of a
   * double), since a couple seat is always fully selected or fully unselected together. */
  private _releaseSlotFor(seatId: string): void {
    const slot = this.slots.find(sl => sl.seatIds.includes(seatId));
    if (!slot) {
      return;
    }
    for (const id of slot.seatIds) {
      const seat = this.seats.find(s => s.id === id);
      if (seat) {
        seat.isSelected = false;
      }
      this.selectedSeats = this.selectedSeats.filter(s => s.id !== id);
      this._session.forget(id);
    }
    slot.seatIds = [];
  }

  private _setLocked(seatId: string, locked: boolean): void {
    const seat = this.seats.find(s => s.id === seatId);
    if (!seat) { return; }
    seat.isLocked = locked;
    if (locked && seat.isSelected) {
      this._releaseSlotFor(seatId);
    }
    this._applyGate();
    this._refreshHoldTimer();
  }

  private _setBooked(seatIds: string[]): void {
    for (const seatId of seatIds) {
      const seat = this.seats.find(s => s.id === seatId);
      if (!seat) { continue; }
      seat.status = PaymentServiceAgent.SeatStatus.Occupied;
      seat.isLocked = false;
      seat.isSelected = false;
      this._releaseSlotFor(seatId);
    }
    this._applyGate();
    this._refreshHoldTimer();
  }

  /** Max orderable quantity for a food/combo: 0 when sold out, the public availability cap when
   * tracked, or null when unlimited (untracked). */
  foodCap(f: CinemaServiceAgent.FoodAndDrinkDTO): number | null {
    return foodOrderCap(f);
  }

  canIncFood(f: CinemaServiceAgent.FoodAndDrinkDTO): boolean {
    const cap = this.foodCap(f);
    return cap === null || (this.foodQty[f.id!] ?? 0) < cap;
  }

  /** Clamps every chosen quantity to its (possibly refreshed) cap. */
  private _clampFoodQty(): void {
    for (const f of this.foods) {
      const cap = this.foodCap(f);
      const qty = this.foodQty[f.id!] ?? 0;
      if (cap !== null && qty > cap) {
        this.foodQty[f.id!] = cap;
      }
    }
  }

  incFood(f: CinemaServiceAgent.FoodAndDrinkDTO): void {
    if (!this.canIncFood(f)) {
      return;
    }
    this.foodQty[f.id!] = (this.foodQty[f.id!] ?? 0) + 1;
  }
  decFood(f: CinemaServiceAgent.FoodAndDrinkDTO): void {
    this.foodQty[f.id!] = Math.max(0, (this.foodQty[f.id!] ?? 0) - 1);
  }

  /** True when this food has no usable imageUrl, or its image already failed to load — the template
   * uses this as the single source of truth so the <img> and the fallback block are never both shown. */
  foodImgHidden(food: CinemaServiceAgent.FoodAndDrinkDTO): boolean {
    if (!food.imageUrl || !food.imageUrl.trim()) {
      return true;
    }
    return this._foodImgFailed.has(food.id!);
  }

  onFoodImgError(food: CinemaServiceAgent.FoodAndDrinkDTO): void {
    if (!food.id) {
      return;
    }
    this._foodImgFailed.add(food.id);
    this._cdr.markForCheck();
  }

  proceedToCheckout(): void {
    if (!this.canProceed) {
      return;
    }
    const state: BookingCheckoutState = {
      showTimeId: this.showTimeId,
      roomId: this.roomId,
      seats: this.selectedSeats.map(s => {
        const category = this.categoryForSeat(s.id!);
        return {
          seatId: s.id!,
          label: seatLabel(s),
          seatKind: s.isDouble ? 'Double' : 'Standard',
          basePrice: s.price ?? 0,
          price: this.seatPrice(s),
          patronCategoryId: category?.patronCategoryId ?? '',
          patronCategoryName: category?.patronCategoryName ?? '',
          seatGroupId: s.seatGroupId ?? undefined,
        };
      }),
      foods: this.selectedFoods.map(f => ({
        foodAndDrinkId: f.id!,
        name: f.name ?? '',
        unitPrice: f.price ?? 0,
        quantity: this.foodQty[f.id!] ?? 0,
      })),
    };
    this._store.select(selectIsAuthenticated).pipe(take(1)).subscribe(isAuthenticated => {
      const queryParams = { showTimeId: this.showTimeId, roomId: this.roomId };
      if (!isAuthenticated) {
        // Park the order, log in, then resume at checkout (it reads the parked order back).
        stashPendingCheckout(state);
        const returnUrl = this._router.serializeUrl(this._router.createUrlTree(['/booking/checkout'], { queryParams }));
        this._router.navigate(['/auth/login'], { queryParams: { returnUrl } });
        return;
      }
      this._navigatingToCheckout = true;
      this._router.navigate(['/booking/checkout'], { state, queryParams })
        .then(
          ok => { if (!ok) { this._navigatingToCheckout = false; } },
          () => { this._navigatingToCheckout = false; },
        );
    });
  }
}

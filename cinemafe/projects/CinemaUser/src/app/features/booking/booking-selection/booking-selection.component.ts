import { ChangeDetectorRef, Component, EventEmitter, Input, OnChanges, OnDestroy, OnInit, Output } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { SharedModule, PaymentServiceAgent, CinemaServiceAgent, BookingHubService } from 'CinemaLib';
import { TranslateService } from '@ngx-translate/core';
import { BookingCheckoutState } from '../booking-checkout/booking-checkout.state';

type SelectableSeat = PaymentServiceAgent.SeatDTO & { isSelected?: boolean; isSelectable?: boolean };

/** One ticket the customer is buying: a category and, once a seat is picked for it, that seat's id. */
interface TicketSlot {
  categoryId: string;
  seatId: string | null;
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
  imports: [SharedModule],
  templateUrl: './booking-selection.component.html',
  styleUrl: './booking-selection.component.scss'
})
export class BookingSelectionComponent implements OnInit, OnChanges, OnDestroy {
  static readonly MAX_TICKETS = 10;
  /** Mirrors BookingManager.IsSeatLocked's 5-minute expiry — keep in sync with the backend. */
  private static readonly LOCK_HOLD_MS = 5 * 60 * 1000;

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
  private _seatLockedAt: Record<string, number> = {};
  private _holdTimer: any;

  /** Patron categories (Adult/Student/Senior/Child) available at this theater, and the quantity
   * chosen per category. Quantities build `slots` — one entry per ticket, in category-declared
   * order — each of which is filled by exactly one seat click. */
  patronCategories: CinemaServiceAgent.PatronCategoryDTO[] = [];
  ticketQty: Record<string, number> = {};
  slots: TicketSlot[] = [];
  categoryWarning = '';

  foods: CinemaServiceAgent.FoodAndDrinkDTO[] = [];
  foodQty: Record<string, number> = {};

  private _subs = new Subscription();
  private _navigatingToCheckout = false;

  constructor(
    private _router: Router,
    private _paymentService: PaymentServiceAgent.HttpService,
    private _cinemaService: CinemaServiceAgent.HttpService,
    private _hub: BookingHubService,
    private _cdr: ChangeDetectorRef,
    private _translate: TranslateService,
  ) {}

  get rows(): string[] {
    return [...new Set(this.seats.map(s => s.rowName!))];
  }

  getSeatsByRow(row: string): SelectableSeat[] {
    return this.seats.filter(s => s.rowName === row).sort((a, b) => (a.colIndex ?? 0) - (b.colIndex ?? 0));
  }

  get maxTickets(): number {
    return BookingSelectionComponent.MAX_TICKETS;
  }

  get totalTickets(): number {
    return this.slots.length;
  }

  get remainingTickets(): number {
    return this.slots.filter(s => s.seatId === null).length;
  }

  get canProceed(): boolean {
    return this.totalTickets > 0 && this.remainingTickets === 0;
  }

  categoryForSeat(seatId: string): CinemaServiceAgent.PatronCategoryDTO | undefined {
    const slot = this.slots.find(sl => sl.seatId === seatId);
    if (!slot) {
      return undefined;
    }
    return this.patronCategories.find(c => c.id === slot.categoryId);
  }

  /** This seat's price after its assigned category's discount — mirrors the server's
   * ApplyPatronDiscount (percent off, floored at 0, rounded to 2dp). */
  seatPrice(seat: SelectableSeat): number {
    const base = seat.price ?? 0;
    const pct = this.categoryForSeat(seat.id!)?.discountPercent ?? 0;
    return Math.round(Math.max(0, base * (1 - pct / 100)) * 100) / 100;
  }

  get totalPrice(): number {
    return this.selectedSeats.reduce((sum, s) => sum + this.seatPrice(s), 0);
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
    this._subs.add(this._hub.seatLocked$.subscribe(e => {
      if (e.connectionId === this._hub.connectionId) { return; } // our own lock — ignore
      this._setLocked(e.seatId, true);
    }));
    this._subs.add(this._hub.seatUnlocked$.subscribe(seatId => this._setLocked(seatId, false)));
    // Our lock attempt lost the race — revert the optimistic selection.
    this._subs.add(this._hub.seatLockFailed$.subscribe(e => this._setLocked(e.seatId, true)));
    // Someone completed a booking — those seats are now permanently unavailable.
    this._subs.add(this._hub.seatBooked$.subscribe(seatIds => this._setBooked(seatIds)));
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
      this._hub.stopConnection();
    }
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
      this._hub.unlockSeat(this._loadedShowTimeId, this._loadedRoomId, s.id!).catch(() => {});
    }
    this._resetSelectionState();
    // stopConnection() also cancels a same-target startConnection() still mid-handshake (SignalR
    // rejects the pending .start()), which _load()'s own .catch(() => {}) swallows — relied on here
    // rather than tracked explicitly, since BookingHubService exposes no "wait for pending start".
    await this._hub.stopConnection();
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
        this._hub.unlockSeat(this._loadedShowTimeId, this._loadedRoomId, s.id!).catch(() => {});
      }
      await this._hub.stopConnection();
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
    this.patronCategories = [];
    this.foods = [];
    this.foodQty = {};
    this.categoryWarning = '';
    this._seatLockedAt = {};
    this._theaterId = '';
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
          if (!room?.theaterId) { return; }
          this._theaterId = room.theaterId;
          this._cinemaService.getFoodAndDrinks(CinemaServiceAgent.PagingSearchDTO.fromJS(
            { pageIndex: 1, pageSize: 100, filters: { theaterId: room.theaterId } }))
            .subscribe(r => {
              if (seq !== this._loadSeq) { return; }
              this.foods = (r.results ?? []).filter(f => f.isAvailable);
              this._cdr.markForCheck();
            });
          this._cinemaService.getPatronCategories(CinemaServiceAgent.PagingSearchDTO.fromJS(
            { pageIndex: 1, pageSize: 100, filters: { theaterId: room.theaterId, isActive: 'true' } }))
            .subscribe(r => {
              if (seq !== this._loadSeq) { return; }
              this.patronCategories = (r.results ?? []).slice().sort((a, b) => (a.discountPercent ?? 0) - (b.discountPercent ?? 0));
              for (const c of this.patronCategories) {
                this.ticketQty[c.id!] = 0;
              }
              this._cdr.markForCheck();
            });
        },
        error: () => {
          if (seq !== this._loadSeq) { return; }
          this._cdr.markForCheck();
        },
      });
    }

    this._hub.startConnection(this.showTimeId, this.roomId).catch(() => { /* degrade to non-realtime */ });
  }

  incTicket(c: CinemaServiceAgent.PatronCategoryDTO): void {
    this.setTicketQty(c.id!, (this.ticketQty[c.id!] ?? 0) + 1);
  }

  decTicket(c: CinemaServiceAgent.PatronCategoryDTO): void {
    this.setTicketQty(c.id!, Math.max(0, (this.ticketQty[c.id!] ?? 0) - 1));
  }

  setTicketQty(categoryId: string, qty: number): void {
    const prev = this.ticketQty[categoryId] ?? 0;
    const otherTotal = this.slots.length - prev;
    const clamped = Math.max(0, Math.min(qty, BookingSelectionComponent.MAX_TICKETS - otherTotal));
    if (clamped === prev) {
      return;
    }
    this.ticketQty[categoryId] = clamped;

    if (clamped > prev) {
      for (let i = prev; i < clamped; i++) {
        this.slots.push({ categoryId, seatId: null });
      }
      this.categoryWarning = '';
    } else {
      const diff = prev - clamped;
      const forThisCategory = this.slots
        .map((slot, index) => ({ slot, index }))
        .filter(x => x.slot.categoryId === categoryId);
      // Free (unassigned) slots go first; assigned slots are freed last-declared-first.
      const removable = [
        ...forThisCategory.filter(x => x.slot.seatId === null).map(x => x.index),
        ...forThisCategory.filter(x => x.slot.seatId !== null).map(x => x.index).reverse(),
      ].slice(0, diff);

      // Release each removed slot's seat AND its double-seat partner (if any) together — a group is
      // always fully selected or fully unselected, same as toggleSeat enforces on selection. The
      // partner's own slot is freed (seatId set back to null) wherever it lives, even if that's a
      // different category's slot; only THIS category's slots are actually spliced out below.
      const removedLabels: string[] = [];
      const releasedSeatIds = new Set<string>();
      for (const index of removable) {
        const slot = this.slots[index];
        if (!slot.seatId) {
          continue;
        }
        const seat = this.seats.find(s => s.id === slot.seatId);
        if (!seat) {
          continue;
        }
        for (const s of this._groupOf(seat)) {
          if (releasedSeatIds.has(s.id!)) {
            continue;
          }
          releasedSeatIds.add(s.id!);
          s.isSelected = false;
          this.selectedSeats = this.selectedSeats.filter(x => x.id !== s.id);
          delete this._seatLockedAt[s.id!];
          this._hub.unlockSeat(this.showTimeId, this.roomId, s.id!).catch(() => {});
          removedLabels.push(`${s.rowName}${s.colIndex}`);
          const holdingSlot = this.slots.find(sl => sl.seatId === s.id);
          if (holdingSlot) {
            holdingSlot.seatId = null;
          }
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

  /** True iff this category's allow-list (empty = unrestricted) permits the given seat type. */
  private _categoryAllows(categoryId: string, seatTypeId: string): boolean {
    const category = this.patronCategories.find(c => c.id === categoryId);
    const allowed = category?.allowedSeatTypeIds ?? [];
    return allowed.length === 0 || allowed.includes(seatTypeId);
  }

  /** Picks the best free slot for a seat of this type: the most-constrained matching category
   * (narrowest allow-list) first, so a restricted slot is never left unfilled in favor of an
   * unrestricted one. Returns -1 when no free slot's category allows this seat type. */
  private _bestSlotFor(seatTypeId: string, exclude: Set<number>): number {
    let bestIndex = -1;
    let bestRank = Infinity;
    this.slots.forEach((slot, index) => {
      if (slot.seatId !== null || exclude.has(index)) {
        return;
      }
      if (!this._categoryAllows(slot.categoryId, seatTypeId)) {
        return;
      }
      const category = this.patronCategories.find(c => c.id === slot.categoryId);
      const allowedLen = category?.allowedSeatTypeIds?.length ?? 0;
      const rank = allowedLen === 0 ? Infinity : allowedLen;
      if (bestIndex === -1 || rank < bestRank) {
        bestRank = rank;
        bestIndex = index;
      }
    });
    return bestIndex;
  }

  /** Recomputes each seat's isAllowedForPatronCategory (does ANY chosen ticket's category allow
   * this seat type at all) and isSelectable (is there currently a FREE slot for it). */
  private _applyGate(): void {
    if (this.seats.length === 0) {
      return;
    }
    for (const seat of this.seats) {
      const seatTypeId = seat.seatTypeId!;
      const allowedByAny = this.slots.length === 0 || this.slots.some(sl => this._categoryAllows(sl.categoryId, seatTypeId));
      const hasFreeMatch = this.slots.some(sl => sl.seatId === null && this._categoryAllows(sl.categoryId, seatTypeId));
      seat.isAllowedForPatronCategory = allowedByAny;
      seat.isSelectable = seat.status === PaymentServiceAgent.SeatStatus.Available && !seat.isLocked && allowedByAny && (!!seat.isSelected || hasFreeMatch);
    }
    this._cdr.markForCheck();
  }

  toggleSeat(seat: SelectableSeat): void {
    if (!seat.isSelected && seat.isSelectable === false) {
      return;
    }
    // Double seats are two linked seats sharing a group id — select/lock them together.
    const group = this._groupOf(seat);
    if (group.some(s => s.status === PaymentServiceAgent.SeatStatus.Occupied || s.isLocked)) {
      return;
    }

    const select = !seat.isSelected;
    if (select) {
      const claimed = new Set<number>();
      const assignments: { seat: SelectableSeat; slotIndex: number }[] = [];
      for (const s of group) {
        const slotIndex = this._bestSlotFor(s.seatTypeId!, claimed);
        if (slotIndex === -1) {
          this.categoryWarning = this._translate.instant(
            group.length > 1 ? 'booking.tickets.notEnoughForDouble' : 'booking.tickets.capReached'
          );
          this._cdr.markForCheck();
          return;
        }
        claimed.add(slotIndex);
        assignments.push({ seat: s, slotIndex });
      }
      for (const { seat: s, slotIndex } of assignments) {
        this.slots[slotIndex].seatId = s.id!;
        s.isSelected = true;
        if (!this.selectedSeats.includes(s)) {
          this.selectedSeats.push(s);
        }
        this._seatLockedAt[s.id!] = Date.now();
        this._hub.lockSeat(this.showTimeId, this.roomId, s.id!).catch(() => { /* see seatLockFailed$ */ });
      }
      this.categoryWarning = '';
    } else {
      for (const s of group) {
        const slot = this.slots.find(sl => sl.seatId === s.id);
        if (slot) {
          slot.seatId = null;
        }
        s.isSelected = false;
        this.selectedSeats = this.selectedSeats.filter(x => x.id !== s.id);
        delete this._seatLockedAt[s.id!];
        this._hub.unlockSeat(this.showTimeId, this.roomId, s.id!).catch(() => {});
      }
    }

    this._applyGate();
    this._refreshHoldTimer();
  }

  /** The soonest lock-expiry timestamp (ms epoch) among currently selected seats, or null if none
   * are selected. That seat's lock is the first to lapse, so it's the one worth counting down to. */
  private _soonestLockExpiry(): number | null {
    const timestamps = this.selectedSeats
      .map(s => this._seatLockedAt[s.id!])
      .filter((t): t is number => t !== undefined);
    if (timestamps.length === 0) {
      return null;
    }
    return Math.min(...timestamps) + BookingSelectionComponent.LOCK_HOLD_MS;
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

  /** The seat plus any others sharing its group id (a double seat); just the seat itself otherwise. */
  private _groupOf(seat: SelectableSeat): SelectableSeat[] {
    if (!seat.seatGroupId) { return [seat]; }
    const group = this.seats.filter(s => s.seatGroupId === seat.seatGroupId);
    if (group.length !== 2) {
      console.warn(`Seat ${seat.id} has an invalid seatGroupId shared by ${group.length} seats; ignoring grouping.`);
      return [seat];
    }
    return group;
  }

  private _setLocked(seatId: string, locked: boolean): void {
    const seat = this.seats.find(s => s.id === seatId);
    if (!seat) { return; }
    seat.isLocked = locked;
    if (locked && seat.isSelected) {
      seat.isSelected = false;
      this.selectedSeats = this.selectedSeats.filter(s => s.id !== seatId);
      delete this._seatLockedAt[seatId];
      const slot = this.slots.find(sl => sl.seatId === seatId);
      if (slot) {
        slot.seatId = null;
      }
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
      delete this._seatLockedAt[seatId];
      const slot = this.slots.find(sl => sl.seatId === seatId);
      if (slot) {
        slot.seatId = null;
      }
    }
    this.selectedSeats = this.selectedSeats.filter(s => !seatIds.includes(s.id!));
    this._applyGate();
    this._refreshHoldTimer();
  }

  incFood(f: CinemaServiceAgent.FoodAndDrinkDTO): void {
    this.foodQty[f.id!] = (this.foodQty[f.id!] ?? 0) + 1;
  }
  decFood(f: CinemaServiceAgent.FoodAndDrinkDTO): void {
    this.foodQty[f.id!] = Math.max(0, (this.foodQty[f.id!] ?? 0) - 1);
  }

  proceedToCheckout(): void {
    if (!this.canProceed) {
      return;
    }
    this._navigatingToCheckout = true;
    const state: BookingCheckoutState = {
      showTimeId: this.showTimeId,
      roomId: this.roomId,
      seats: this.selectedSeats.map(s => {
        const category = this.categoryForSeat(s.id!);
        return {
          seatId: s.id!,
          label: `${s.rowName}${s.colIndex}`,
          seatTypeName: s.seatTypeName ?? '',
          basePrice: s.price ?? 0,
          price: this.seatPrice(s),
          patronCategoryId: category?.id ?? '',
          patronCategoryName: category?.name ?? '',
          discountPercent: category?.discountPercent ?? 0,
        };
      }),
      foods: this.selectedFoods.map(f => ({
        foodAndDrinkId: f.id!,
        name: f.name ?? '',
        unitPrice: f.price ?? 0,
        quantity: this.foodQty[f.id!] ?? 0,
      })),
    };
    this._router.navigate(['/booking/checkout'], { state, queryParams: { showTimeId: this.showTimeId, roomId: this.roomId } })
      .then(
        ok => { if (!ok) { this._navigatingToCheckout = false; } },
        () => { this._navigatingToCheckout = false; },
      );
  }
}

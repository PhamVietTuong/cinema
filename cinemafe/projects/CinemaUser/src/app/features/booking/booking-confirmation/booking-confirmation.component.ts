import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { SharedModule, PaymentServiceAgent, CinemaServiceAgent, IdentityServiceAgent, BookingHubService } from 'CinemaLib';
import { TranslateService } from '@ngx-translate/core';
import * as QRCode from 'qrcode';

type SelectableSeat = PaymentServiceAgent.SeatDTO & { isSelected?: boolean };

@Component({
  selector: 'app-booking-confirmation',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './booking-confirmation.component.html',
  styleUrl: './booking-confirmation.component.scss'
})
export class BookingConfirmationComponent implements OnInit, OnDestroy {
  seats: SelectableSeat[] = [];

  /** Client-side seat-hold countdown. By this page the booking already exists as a Pending invoice,
   * so the seats are held until PendingBookingReaper sweeps it — keep this in step with that service's
   * 15-minute hold window. (The realtime lock's 5 minutes governs seat *selection*, one step earlier;
   * counting down from 5 here told the customer their seats were gone while they were still held.)
   * If it does lapse and someone else takes a seat, the booking is rejected server-side by the
   * active-seat unique index, so we warn rather than block. */
  holdSecondsLeft = 15 * 60;
  holdExpired = false;
  private _holdTimer: any;
  showTimeId = '';
  roomId = '';
  paymentMethod = 'Card';
  loading = false;
  error = '';
  bookingSuccess = false;
  bookingCode = '';
  qrDataUrl = '';

  /** Concession items for this theater + the quantity the user picked (keyed by id). */
  foods: CinemaServiceAgent.FoodAndDrinkDTO[] = [];
  foodQty: Record<string, number> = {};
  discountCode = '';

  /** Gift-card code entered at checkout. The server applies the actual discount. */
  giftCardCode = '';
  /** Result of the last gift-card validation (null = not yet checked). */
  giftCardValid: boolean | null = null;
  giftCardMessage = '';
  giftCardChecking = false;

  /** Patron categories (Adult/Student/Senior/Child) available at this theater, and the category
   * chosen per seat (seatId -> categoryId). Self-reported; checked visually at the theater. */
  patronCategories: CinemaServiceAgent.PatronCategoryDTO[] = [];
  categoryBySeat: Record<string, string> = {};

  /** 1 loyalty point == 1000 VND when redeemed. */
  static readonly POINT_VALUE = 1000;
  /** Available loyalty-points balance for the signed-in user. */
  pointsBalance = 0;
  /** How many points the user chose to redeem on this order. */
  pointsToRedeem = 0;
  /** How many points the server actually applied (shown on the success screen). */
  pointsRedeemed = 0;

  constructor(
    private _router: Router,
    private _paymentService: PaymentServiceAgent.HttpService,
    private _cinemaService: CinemaServiceAgent.HttpService,
    private _identityService: IdentityServiceAgent.HttpService,
    private _bookingHub: BookingHubService,
    private _cdr: ChangeDetectorRef,
    private _translate: TranslateService,
  ) {}

  get totalPrice(): number {
    return this.seats.reduce((sum, s) => sum + this.seatPrice(s), 0);
  }

  /** This seat's price after its chosen patron category's discount — mirrors the server's
   * ApplyPatronDiscount (percent off, floored at 0, rounded to 2dp) so the total shown here
   * matches what CreateBooking persists. */
  seatPrice(seat: SelectableSeat): number {
    const base = seat.price ?? 0;
    const pct = this.categoryFor(seat.id!)?.discountPercent ?? 0;
    return Math.round(Math.max(0, base * (1 - pct / 100)) * 100) / 100;
  }

  categoryFor(seatId: string): CinemaServiceAgent.PatronCategoryDTO | undefined {
    return this.patronCategories.find(c => c.id === this.categoryBySeat[seatId]);
  }

  onCategoryChange(seatId: string, categoryId: string): void {
    this.categoryBySeat[seatId] = categoryId;
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

  /**
   * Largest number of points that may be redeemed on this order: capped by the
   * user's balance and by the order total (points can't discount below 0).
   */
  get maxRedeemablePoints(): number {
    return Math.min(this.pointsBalance, Math.floor(this.grandTotal / BookingConfirmationComponent.POINT_VALUE));
  }

  /** VND discount from the points the user currently chose to redeem. */
  get pointsDiscount(): number {
    return (this.pointsToRedeem || 0) * BookingConfirmationComponent.POINT_VALUE;
  }

  /** Order total after the points discount (mirrors the server flooring at 0). */
  get finalTotal(): number {
    return Math.max(0, this.grandTotal - this.pointsDiscount);
  }

  ngOnInit(): void {
    const state = history.state as any;
    if (state) {
      this.seats = state.seats ?? [];
      this.showTimeId = state.showTimeId;
      this.roomId = state.roomId;
    }
    this._startHoldCountdown();
    // Load the signed-in user's loyalty balance so they can redeem points.
    this._identityService.getProfile().subscribe({
      next: u => { this.pointsBalance = u.points ?? 0; this._cdr.markForCheck(); },
      error: () => this._cdr.markForCheck(),
    });
    // Load this theater's concessions and patron categories so the customer can add combos and
    // pick a per-seat pricing category (Adult/Student/Senior/Child) on the order.
    if (this.roomId) {
      this._cinemaService.getRoom(this.roomId).subscribe(room => {
        if (!room?.theaterId) { return; }
        this._cinemaService.getFoodAndDrinks(CinemaServiceAgent.PagingSearchDTO.fromJS(
          { pageIndex: 1, pageSize: 100, filters: { theaterId: room.theaterId } }))
          .subscribe(r => {
            this.foods = (r.results ?? []).filter(f => f.isAvailable);
            this._cdr.markForCheck();
          });
        this._cinemaService.getPatronCategories(CinemaServiceAgent.PagingSearchDTO.fromJS(
          { pageIndex: 1, pageSize: 100, filters: { theaterId: room.theaterId, isActive: 'true' } }))
          .subscribe(r => {
            this.patronCategories = (r.results ?? []).slice().sort((a, b) => (a.discountPercent ?? 0) - (b.discountPercent ?? 0));
            // Default every seat to the lowest-discount category (Adult / full price).
            const defaultId = this.patronCategories[0]?.id;
            if (defaultId) {
              for (const s of this.seats) {
                this.categoryBySeat[s.id!] = defaultId;
              }
            }
            this._cdr.markForCheck();
          });
      });
    }
  }

  ngOnDestroy(): void {
    if (this._holdTimer) {
      clearInterval(this._holdTimer);
    }
  }

  private _startHoldCountdown(): void {
    this._holdTimer = setInterval(() => {
      if (this.holdSecondsLeft > 0) {
        this.holdSecondsLeft--;
      } else {
        this.holdExpired = true;
        clearInterval(this._holdTimer);
      }
      this._cdr.markForCheck();
    }, 1000);
  }

  /** The seat-hold countdown as MM:SS. */
  get holdCountdown(): string {
    const m = Math.floor(this.holdSecondsLeft / 60);
    const s = this.holdSecondsLeft % 60;
    return `${m}:${s.toString().padStart(2, '0')}`;
  }

  incFood(f: CinemaServiceAgent.FoodAndDrinkDTO): void {
    this.foodQty[f.id!] = (this.foodQty[f.id!] ?? 0) + 1;
  }
  decFood(f: CinemaServiceAgent.FoodAndDrinkDTO): void {
    this.foodQty[f.id!] = Math.max(0, (this.foodQty[f.id!] ?? 0) - 1);
  }

  /** Keep the redeem input within [0, maxRedeemablePoints] as a whole number. */
  clampPoints(): void {
    const n = Math.floor(this.pointsToRedeem || 0);
    this.pointsToRedeem = Math.max(0, Math.min(n, this.maxRedeemablePoints));
  }

  /** Check the entered gift-card code and show its balance/message. */
  validateGiftCard(): void {
    const code = this.giftCardCode.trim();
    if (!code) {
      this.giftCardValid = null;
      this.giftCardMessage = '';
      return;
    }
    this.giftCardChecking = true;
    this._paymentService.validateGiftCard(PaymentServiceAgent.ValidateGiftCardRequest.fromJS({ code }))
      .subscribe({
        next: res => {
          this.giftCardValid = !!res?.valid;
          this.giftCardMessage = res?.message
            ?? (res?.valid ? this._translate.instant('booking.summary.giftCardBalance', { balance: res?.balance ?? 0 }) : '');
          this.giftCardChecking = false;
          this._cdr.markForCheck();
        },
        error: err => {
          this.giftCardValid = false;
          this.giftCardMessage = this._err(err, this._translate.instant('booking.summary.giftCardInvalid'));
          this.giftCardChecking = false;
          this._cdr.markForCheck();
        },
      });
  }

  confirmBooking(): void {
    this.clampPoints();
    this.loading = true;
    this.error = '';
    const foods = this.foods
      .filter(f => (this.foodQty[f.id!] ?? 0) > 0)
      .map(f => PaymentServiceAgent.BookingFoodItem.fromJS({ foodAndDrinkId: f.id, quantity: this.foodQty[f.id!] }));
    const request = PaymentServiceAgent.CreateBookingRequest.fromJS({
      showTimeId: this.showTimeId,
      roomId: this.roomId,
      // Price is derived server-side from each seat's type multiplier and its chosen patron category.
      seats: this.seats.map(s => PaymentServiceAgent.BookingSeatItem.fromJS({
        seatId: s.id,
        patronCategoryId: this.categoryBySeat[s.id!] || undefined,
      })),
      foods,
      discountCode: this.discountCode.trim() || undefined,
      giftCardCode: this.giftCardCode.trim() || undefined,
      paymentMethod: this.paymentMethod,
      pointsToRedeem: this.pointsToRedeem || undefined,
      // Pass our live hub connection id so the server ignores our own held seats when enforcing locks.
      connectionId: this._bookingHub.connectionId ?? undefined,
    });
    this._paymentService.createBooking(request).subscribe({
      next: res => {
        this.pointsRedeemed = res?.pointsRedeemed ?? 0;
        const invoiceId = res?.invoiceId;
        const code = res?.invoiceCode ?? res?.invoiceId ?? '';
        if (!invoiceId) {
          // No invoice reference to pay against — just show what we have.
          this._showSuccess(code);
          return;
        }
        this._initiatePayment(invoiceId, code);
      },
      error: err => { this.error = this._err(err, this._translate.instant('booking.errors.bookingFailed')); this.loading = false; this._cdr.markForCheck(); },
    });
  }

  /** Maps the selected payment-method tile to a backend payment provider name. */
  private _providerFor(method: string): string {
    switch (method) {
      case 'Momo': {
        return 'MoMo';
      }
      case 'ApplePay':
      case 'GooglePay': {
        return 'Stripe';
      }
      case 'Card': {
        return 'VNPay';
      }
      default: {
        return 'Sandbox';
      }
    }
  }

  /** Kick off payment: redirect to the gateway, or confirm inline for Sandbox. */
  private _initiatePayment(invoiceId: string, code: string): void {
    const returnUrl = `${window.location.origin}/booking/payment-return?invoiceId=${encodeURIComponent(invoiceId)}`;
    const request = PaymentServiceAgent.InitiatePaymentRequest.fromJS({
      invoiceId,
      provider: this._providerFor(this.paymentMethod),
      returnUrl,
    });
    this._paymentService.initiatePayment(request).subscribe({
      next: init => {
        if (init?.alreadyPaid) {
          // Gift card covered the full invoice — nothing to pay, go straight to the e-ticket.
          this._showSuccess(code);
          return;
        }
        if (init?.redirectUrl) {
          // Real gateway: hand the browser over to the hosted checkout page.
          window.location.href = init.redirectUrl;
          return;
        }
        // Sandbox (no redirect): confirm right away and show the e-ticket.
        this._confirmSandbox(invoiceId, init?.paymentReference ?? '', code);
      },
      error: err => { this.error = this._err(err, this._translate.instant('booking.errors.paymentFailed')); this.loading = false; this._cdr.markForCheck(); },
    });
  }

  private _confirmSandbox(invoiceId: string, paymentReference: string, code: string): void {
    const request = PaymentServiceAgent.ConfirmPaymentRequest.fromJS({ invoiceId, paymentReference });
    this._paymentService.confirmPayment(request).subscribe({
      next: () => { this._showSuccess(code); },
      error: err => { this.error = this._err(err, this._translate.instant('booking.errors.paymentFailed')); this.loading = false; this._cdr.markForCheck(); },
    });
  }

  private _showSuccess(code: string): void {
    this.bookingCode = code;
    this.bookingSuccess = true;
    this.loading = false;
    this._cdr.markForCheck();
    // Render a real, scannable QR of the booking reference for the e-ticket.
    if (this.bookingCode) {
      QRCode.toDataURL(this.bookingCode, { margin: 1, width: 200 })
        .then(url => { this.qrDataUrl = url; this._cdr.markForCheck(); })
        .catch(() => { /* keep the icon fallback */ });
    }
  }

  private _err(e: any, fallback: string): string {
    const x = e?.error;
    return (typeof x === 'string' && x) ? x : (x?.error || x?.message || fallback);
  }

  goHome(): void {
    this._router.navigate(['/']);
  }

  goProfile(): void {
    this._router.navigate(['/profile']);
  }
}

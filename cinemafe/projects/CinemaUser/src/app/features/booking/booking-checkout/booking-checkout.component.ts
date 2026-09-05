import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { SharedModule, PaymentServiceAgent, IdentityServiceAgent, BookingHubService } from 'CinemaLib';
import { TranslateService } from '@ngx-translate/core';
import * as QRCode from 'qrcode';
import { BookingCheckoutSeat, BookingCheckoutFood, BookingCheckoutState } from './booking-checkout.state';

/**
 * Second page of the booking flow: payment. Reached only via BookingPageComponent's
 * `proceedToCheckout()`, which hands over the chosen seats/food as router navigation state — this
 * page never re-fetches seats/categories, it just prices and pays what arrived in that state.
 */
@Component({
  selector: 'app-booking-checkout',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './booking-checkout.component.html',
  styleUrl: './booking-checkout.component.scss'
})
export class BookingCheckoutComponent implements OnInit, OnDestroy {
  seats: BookingCheckoutSeat[] = [];
  foods: BookingCheckoutFood[] = [];
  showTimeId = '';
  roomId = '';

  /** Client-side seat-hold countdown, started on arrival here — this mirrors
   * PendingBookingReaper's 15-minute hold window, which only begins once the booking exists as a
   * Pending invoice (i.e. from this page onward). The SignalR seat lock, started on page 1 and
   * carried over by NOT stopping the hub connection during navigation, is what protects the seats
   * up to this point. */
  holdSecondsLeft = 15 * 60;
  holdExpired = false;
  private _holdTimer: any;

  paymentMethod = 'Card';
  loading = false;
  error = '';
  bookingSuccess = false;
  bookingCode = '';
  qrDataUrl = '';

  discountCode = '';
  giftCardCode = '';
  giftCardValid: boolean | null = null;
  giftCardMessage = '';
  giftCardChecking = false;

  static readonly POINT_VALUE = 1000;
  pointsBalance = 0;
  pointsToRedeem = 0;
  pointsRedeemed = 0;

  constructor(
    private _route: ActivatedRoute,
    private _router: Router,
    private _paymentService: PaymentServiceAgent.HttpService,
    private _identityService: IdentityServiceAgent.HttpService,
    private _hub: BookingHubService,
    private _cdr: ChangeDetectorRef,
    private _translate: TranslateService,
  ) {}

  get totalPrice(): number {
    return this.seats.reduce((sum, s) => sum + s.price, 0);
  }

  get foodTotal(): number {
    return this.foods.reduce((sum, f) => sum + f.unitPrice * f.quantity, 0);
  }

  get grandTotal(): number {
    return this.totalPrice + this.foodTotal;
  }

  get maxRedeemablePoints(): number {
    return Math.min(this.pointsBalance, Math.floor(this.grandTotal / BookingCheckoutComponent.POINT_VALUE));
  }

  get pointsDiscount(): number {
    return (this.pointsToRedeem || 0) * BookingCheckoutComponent.POINT_VALUE;
  }

  get finalTotal(): number {
    return Math.max(0, this.grandTotal - this.pointsDiscount);
  }

  ngOnInit(): void {
    const state = history.state as Partial<BookingCheckoutState> | undefined;
    if (!state?.seats?.length) {
      // Reload/deep-link with no order in flight — never render an empty checkout.
      const showTimeId = this._route.snapshot.queryParams['showTimeId'];
      const roomId = this._route.snapshot.queryParams['roomId'];
      if (showTimeId && roomId) {
        this._router.navigate(['/booking/seats'], { queryParams: { showTimeId, roomId } });
      } else {
        this._router.navigate(['/']);
      }
      return;
    }

    this.showTimeId = state.showTimeId ?? '';
    this.roomId = state.roomId ?? '';
    this.seats = state.seats;
    this.foods = state.foods ?? [];

    this._startHoldCountdown();

    this._identityService.getProfile().subscribe({
      next: u => { this.pointsBalance = u.points ?? 0; this._cdr.markForCheck(); },
      error: () => this._cdr.markForCheck(),
    });
  }

  ngOnDestroy(): void {
    if (this._holdTimer) {
      clearInterval(this._holdTimer);
    }
    // Always release the hub connection (and the seat locks it holds) on the way out — including
    // "Back to seats", since BookingHubService.startConnection always opens a NEW connection rather
    // than reusing one, so a kept-alive connection here would never be released (its locks would
    // never expire in GetSeatsAsync either) and page 1 re-locks fine on a fresh connection anyway.
    this._hub.stopConnection();
  }

  backToSeats(): void {
    this._router.navigate(['/booking/seats'], { queryParams: { showTimeId: this.showTimeId, roomId: this.roomId } });
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

  get holdCountdown(): string {
    const m = Math.floor(this.holdSecondsLeft / 60);
    const s = this.holdSecondsLeft % 60;
    return `${m}:${s.toString().padStart(2, '0')}`;
  }

  clampPoints(): void {
    const n = Math.floor(this.pointsToRedeem || 0);
    this.pointsToRedeem = Math.max(0, Math.min(n, this.maxRedeemablePoints));
  }

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
    const foods = this.foods.map(f => PaymentServiceAgent.BookingFoodItem.fromJS({
      foodAndDrinkId: f.foodAndDrinkId,
      quantity: f.quantity,
    }));
    const request = PaymentServiceAgent.CreateBookingRequest.fromJS({
      showTimeId: this.showTimeId,
      roomId: this.roomId,
      // Price is derived server-side from each seat's type multiplier and its own patron category.
      seats: this.seats.map(s => PaymentServiceAgent.BookingSeatItem.fromJS({
        seatId: s.seatId,
        patronCategoryId: s.patronCategoryId || undefined,
      })),
      foods,
      discountCode: this.discountCode.trim() || undefined,
      giftCardCode: this.giftCardCode.trim() || undefined,
      paymentMethod: this.paymentMethod,
      pointsToRedeem: this.pointsToRedeem || undefined,
      // Pass our live hub connection id (carried over from page 1) so the server ignores our own
      // held seats when enforcing locks.
      connectionId: this._hub.connectionId ?? undefined,
    });
    this._paymentService.createBooking(request).subscribe({
      next: res => {
        this.pointsRedeemed = res?.pointsRedeemed ?? 0;
        const invoiceId = res?.invoiceId;
        const code = res?.invoiceCode ?? res?.invoiceId ?? '';
        if (!invoiceId) {
          this._showSuccess(code);
          return;
        }
        this._initiatePayment(invoiceId, code);
      },
      error: err => { this.error = this._err(err, this._translate.instant('booking.errors.bookingFailed')); this.loading = false; this._cdr.markForCheck(); },
    });
  }

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
          this._showSuccess(code);
          return;
        }
        if (init?.redirectUrl) {
          window.location.href = init.redirectUrl;
          return;
        }
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

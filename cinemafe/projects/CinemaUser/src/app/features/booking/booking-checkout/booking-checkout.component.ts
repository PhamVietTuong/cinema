import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { SharedModule, PaymentServiceAgent, IdentityServiceAgent, CinemaServiceAgent, BookingHubService, seatKindLabel } from 'CinemaLib';
import { MatDialog } from '@angular/material/dialog';
import { Observable, catchError, map, of } from 'rxjs';
import { TranslateService } from '@ngx-translate/core';
import * as QRCode from 'qrcode';
import { DiscountCodeDialog, DiscountCheckResult, DiscountCodeDialogResult } from './discount-code.dialog';
import { PointsRedeemDialog } from './points-redeem.dialog';
import { GiftCardDialog, GiftCardCheckResult, GiftCardDialogResult } from './gift-card.dialog';
import { BookingCheckoutSeat, BookingCheckoutFood, BookingCheckoutState, takePendingCheckout } from './booking-checkout.state';

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
  seatKindLabel(seat: { seatKind: string }): string {
    return seatKindLabel(seat.seatKind === 'Double');
  }

  seats: BookingCheckoutSeat[] = [];
  foods: BookingCheckoutFood[] = [];
  showTimeId = '';
  roomId = '';

  // Mirrors PendingBookingReaper's hold window server-side.
  private static readonly HOLD_SECONDS = 15 * 60;

  /** Client-side seat-hold countdown, started on arrival here — this mirrors
   * PendingBookingReaper's 15-minute hold window, which only begins once the booking exists as a
   * Pending invoice (i.e. from this page onward). The SignalR seat lock, started on page 1 and
   * carried over by NOT stopping the hub connection during navigation, is what protects the seats
   * up to this point. */
  holdSecondsLeft = BookingCheckoutComponent.HOLD_SECONDS;
  holdExpired = false;
  private _holdTimer: any;

  paymentMethod = 'Card';
  loading = false;
  error = '';
  bookingSuccess = false;
  bookingCode = '';
  qrDataUrl = '';

  /** Payment options in display order; `svg` picks a custom svg-icon, otherwise `icon` is a Material icon. */
  readonly methods: { value: string; labelKey: string; svg?: string; icon?: string }[] = [
    { value: 'Momo', labelKey: 'booking.confirm.momoWallet', icon: 'account_balance_wallet' },
    { value: 'Card', labelKey: 'booking.confirm.domesticCard', icon: 'credit_card' },
    { value: 'ApplePay', labelKey: 'booking.confirm.applePay', svg: 'apple' },
    { value: 'GooglePay', labelKey: 'booking.confirm.googlePay', svg: 'google' },
  ];

  // Order context for the summary card, loaded after arrival; blank until each lookup answers.
  movieTitle = '';
  ageCode = '';
  theaterName = '';
  theaterAddress = '';
  roomName = '';
  startTime: Date | null = null;

  discountCode = '';
  discountValid: boolean | null = null;
  discountMessage = '';
  discountAmount = 0;
  giftCardCode = '';
  giftCardValid: boolean | null = null;
  giftCardMessage = '';

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
    private _cinemaService: CinemaServiceAgent.HttpService,
    private _dialog: MatDialog,
  ) {}

  get totalPrice(): number {
    return this.seats.reduce((sum, s) => sum + s.price, 0);
  }

  /** One display line per ticket: a Double pair's two halved prices are summed back into the
   * single whole-seat price the customer was quoted, instead of showing two confusing half lines. */
  get ticketLines(): { label: string; seatKind: string; categoryName: string; price: number }[] {
    const groups = new Map<string, BookingCheckoutSeat[]>();
    for (const s of this.seats) {
      const key = s.seatGroupId ?? s.seatId;
      const group = groups.get(key);
      if (group) {
        group.push(s);
      } else {
        groups.set(key, [s]);
      }
    }
    return [...groups.values()].map(group => ({
      label: group.map(s => s.label).join('-'),
      seatKind: group[0].seatKind,
      categoryName: group[0].patronCategoryName,
      price: group.reduce((sum, s) => sum + s.price, 0),
    }));
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
    let state = history.state as Partial<BookingCheckoutState> | undefined;
    if (!state?.seats?.length) {
      // Resuming after a login redirect: the order was parked in sessionStorage.
      state = takePendingCheckout();
    }
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
    this._loadOrderContext();

    this._identityService.getProfile().subscribe({
      next: u => { this.pointsBalance = u.points ?? 0; this._cdr.markForCheck(); },
      error: () => this._cdr.markForCheck(),
    });
  }

  /** Asks the server whether a promo code can be used for this order; failures come back as an invalid result. */
  checkDiscountCode(code: string): Observable<DiscountCheckResult> {
    return this._paymentService.validateDiscountCode(PaymentServiceAgent.ValidateDiscountCodeRequest.fromJS({
      code,
      roomId: this.roomId,
      showTimeId: this.showTimeId,
      total: this.grandTotal,
    })).pipe(
      map(res => {
        const amount = res?.discountAmount ?? 0;
        return {
          valid: !!res?.valid,
          discountAmount: amount,
          message: res?.valid
            ? this._translate.instant('booking.summary.discountSaves', { code, amount: Math.round(amount).toLocaleString('en-US') })
            : (res?.message ?? this._translate.instant('booking.summary.discountInvalid')),
        };
      }),
      catchError(err => of({
        valid: false,
        discountAmount: 0,
        message: this._err(err, this._translate.instant('booking.summary.discountInvalid')),
      })),
    );
  }

  openDiscountDialog(): void {
    this._dialog.open(DiscountCodeDialog, {
      data: { code: this.discountCode, check: (code: string) => this.checkDiscountCode(code) },
      width: '420px',
      maxWidth: '92vw',
    }).afterClosed().subscribe((result: DiscountCodeDialogResult | undefined) => {
      if (result) {
        this.discountCode = result.code;
        this.discountValid = result.valid;
        this.discountMessage = result.message;
        this.discountAmount = result.discountAmount;
        this._cdr.markForCheck();
      }
    });
  }

  openPointsDialog(): void {
    this._dialog.open(PointsRedeemDialog, {
      data: {
        balance: this.pointsBalance,
        max: this.maxRedeemablePoints,
        value: this.pointsToRedeem,
        pointValue: BookingCheckoutComponent.POINT_VALUE,
      },
      width: '420px',
      maxWidth: '92vw',
    }).afterClosed().subscribe((points: number | undefined) => {
      if (points !== undefined) {
        this.pointsToRedeem = points;
        this.clampPoints();
        this._cdr.markForCheck();
      }
    });
  }

  /** Best-effort display data only: a failed lookup leaves its fields blank and never blocks payment. */
  private _loadOrderContext(): void {
    this._cinemaService.getShowTime(this.showTimeId).subscribe({
      next: st => {
        this.startTime = st.startTime ?? null;
        this.roomName = st.roomName ?? '';
        this._cdr.markForCheck();
        if (st.movieId) {
          this._cinemaService.getMovie(st.movieId).subscribe({
            next: m => {
              this.movieTitle = m.title ?? '';
              this.ageCode = m.ageRestrictionCode ?? '';
              this._cdr.markForCheck();
            },
            error: () => { /* summary stays without a title */ },
          });
        }
      },
      error: () => { /* summary stays without showtime details */ },
    });
    this._cinemaService.getRoom(this.roomId).subscribe({
      next: room => {
        this.roomName = this.roomName || (room.name ?? '');
        this._cdr.markForCheck();
        if (room.theaterId) {
          this._cinemaService.getTheater(room.theaterId).subscribe({
            next: t => {
              this.theaterName = t.name ?? '';
              this.theaterAddress = t.address ?? '';
              this._cdr.markForCheck();
            },
            error: () => { /* summary stays without the cinema */ },
          });
        }
      },
      error: () => { /* summary stays without the room */ },
    });
  }

  /** Distinct ticket categories in the order, e.g. "Adult, Student". */
  get ticketTypes(): string {
    return [...new Set(this.ticketLines.map(t => t.categoryName))].join(', ');
  }

  get seatCodes(): string {
    return this.ticketLines.map(t => t.label).join(', ');
  }

  get foodSummary(): string {
    return this.foods.map(f => `${f.quantity} × ${f.name}`).join(' · ');
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

  /** Asks the server whether a gift card code is usable; failures come back as an invalid result, never an error. */
  checkGiftCard(code: string): Observable<GiftCardCheckResult> {
    return this._paymentService.validateGiftCard(PaymentServiceAgent.ValidateGiftCardRequest.fromJS({ code })).pipe(
      map(res => ({
        valid: !!res?.valid,
        message: res?.message
          ?? (res?.valid ? this._translate.instant('booking.summary.giftCardBalance', { balance: res?.balance ?? 0 }) : ''),
      })),
      catchError(err => of({
        valid: false,
        message: this._err(err, this._translate.instant('booking.summary.giftCardInvalid')),
      })),
    );
  }

  openGiftCardDialog(): void {
    this._dialog.open(GiftCardDialog, {
      data: { code: this.giftCardCode, check: (code: string) => this.checkGiftCard(code) },
      width: '460px',
      maxWidth: '92vw',
    }).afterClosed().subscribe((result: GiftCardDialogResult | undefined) => {
      if (result) {
        this.giftCardCode = result.code;
        this.giftCardValid = result.valid;
        this.giftCardMessage = result.message;
        this._cdr.markForCheck();
      }
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

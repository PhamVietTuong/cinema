import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { SharedModule, PaymentServiceAgent, CinemaServiceAgent } from 'CinemaLib';
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
export class BookingConfirmationComponent implements OnInit {
  seats: SelectableSeat[] = [];
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

  constructor(
    private _router: Router,
    private _paymentService: PaymentServiceAgent.HttpService,
    private _cinemaService: CinemaServiceAgent.HttpService,
    private _cdr: ChangeDetectorRef,
    private _translate: TranslateService,
  ) {}

  get totalPrice(): number {
    return this.seats.reduce((sum, s) => sum + (s.price ?? 0), 0);
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

  ngOnInit(): void {
    const state = history.state as any;
    if (state) {
      this.seats = state.seats ?? [];
      this.showTimeId = state.showTimeId;
      this.roomId = state.roomId;
    }
    // Load this theater's concessions so the customer can add combos to the order.
    if (this.roomId) {
      this._cinemaService.getRoom(this.roomId).subscribe(room => {
        if (!room?.theaterId) { return; }
        this._cinemaService.getFoodAndDrinks(CinemaServiceAgent.PagingSearchDTO.fromJS(
          { pageIndex: 1, pageSize: 100, filters: { theaterId: room.theaterId } }))
          .subscribe(r => {
            this.foods = (r.results ?? []).filter(f => f.isAvailable);
            this._cdr.markForCheck();
          });
      });
    }
  }

  incFood(f: CinemaServiceAgent.FoodAndDrinkDTO): void {
    this.foodQty[f.id!] = (this.foodQty[f.id!] ?? 0) + 1;
  }
  decFood(f: CinemaServiceAgent.FoodAndDrinkDTO): void {
    this.foodQty[f.id!] = Math.max(0, (this.foodQty[f.id!] ?? 0) - 1);
  }

  confirmBooking(): void {
    this.loading = true;
    this.error = '';
    const foods = this.foods
      .filter(f => (this.foodQty[f.id!] ?? 0) > 0)
      .map(f => PaymentServiceAgent.BookingFoodItem.fromJS({ foodAndDrinkId: f.id, quantity: this.foodQty[f.id!] }));
    const request = PaymentServiceAgent.CreateBookingRequest.fromJS({
      showTimeId: this.showTimeId,
      roomId: this.roomId,
      // Price is derived server-side from each seat's type multiplier — no ticket type needed.
      seats: this.seats.map(s => PaymentServiceAgent.BookingSeatItem.fromJS({ seatId: s.id })),
      foods,
      discountCode: this.discountCode.trim() || undefined,
      paymentMethod: this.paymentMethod,
    });
    this._paymentService.createBooking(request).subscribe({
      next: res => {
        this.bookingCode = res?.invoiceCode ?? res?.invoiceId ?? '';
        this.bookingSuccess = true;
        this.loading = false;
        this._cdr.markForCheck();
        // Render a real, scannable QR of the booking reference for the e-ticket.
        if (this.bookingCode) {
          QRCode.toDataURL(this.bookingCode, { margin: 1, width: 200 })
            .then(url => { this.qrDataUrl = url; this._cdr.markForCheck(); })
            .catch(() => { /* keep the icon fallback */ });
        }
      },
      error: err => { this.error = this._err(err, this._translate.instant('booking.errors.bookingFailed')); this.loading = false; this._cdr.markForCheck(); },
    });
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

import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { SharedModule, PaymentServiceAgent } from 'CinemaLib';
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

  constructor(
    private _router: Router,
    private _paymentService: PaymentServiceAgent.HttpService,
    private _cdr: ChangeDetectorRef,
  ) {}

  get totalPrice(): number {
    return this.seats.reduce((sum, s) => sum + (s.price ?? 0), 0);
  }

  get grandTotal(): number {
    return this.totalPrice + 10000;
  }

  ngOnInit(): void {
    const state = history.state as any;
    if (state) {
      this.seats = state.seats ?? [];
      this.showTimeId = state.showTimeId;
      this.roomId = state.roomId;
    }
  }

  confirmBooking(): void {
    this.loading = true;
    this.error = '';
    const request = PaymentServiceAgent.CreateBookingRequest.fromJS({
      showTimeId: this.showTimeId,
      roomId: this.roomId,
      // Price is derived server-side from each seat's type multiplier — no ticket type needed.
      seats: this.seats.map(s => PaymentServiceAgent.BookingSeatItem.fromJS({ seatId: s.id })),
      foods: [],
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
      error: err => { this.error = this._err(err, 'Đặt vé thất bại. Vui lòng thử lại.'); this.loading = false; this._cdr.markForCheck(); },
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

import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { SharedModule, PaymentServiceAgent } from 'CinemaLib';

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
  paymentMethod = 'VNPay';
  loading = false;
  error = '';
  bookingSuccess = false;
  bookingCode = '';

  constructor(
    private _router: Router,
    private _paymentService: PaymentServiceAgent.HttpService,
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
    const request = PaymentServiceAgent.CreateBookingRequest.fromJS({
      showTimeId: this.showTimeId,
      roomId: this.roomId,
      seats: this.seats.map(s => PaymentServiceAgent.BookingSeatItem.fromJS({ seatId: s.id, ticketTypeId: 1 })),
      foods: [],
      paymentMethod: this.paymentMethod,
    });
    this._paymentService.createBooking(request).subscribe({
      next: (res: any) => {
        this.bookingCode = res?.bookingCode ?? res?.id ?? 'MP' + Date.now().toString(36).toUpperCase();
        this.bookingSuccess = true;
        this.loading = false;
      },
      error: err => { this.error = err.error?.error ?? 'Đặt vé thất bại. Vui lòng thử lại.'; this.loading = false; }
    });
  }

  goHome(): void {
    this._router.navigate(['/']);
  }

  goProfile(): void {
    this._router.navigate(['/profile']);
  }
}

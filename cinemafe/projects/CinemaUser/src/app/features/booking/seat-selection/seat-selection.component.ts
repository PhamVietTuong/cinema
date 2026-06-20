import { ChangeDetectorRef, Component, OnInit, OnDestroy } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { NgFor, NgIf, NgClass, CurrencyPipe } from '@angular/common';
import { Subscription } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { SharedModule, PaymentServiceAgent, BookingHubService } from 'CinemaLib';

type SelectableSeat = PaymentServiceAgent.SeatDTO & { isSelected?: boolean };

@Component({
  selector: 'app-seat-selection',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './seat-selection.component.html',
  styleUrl: './seat-selection.component.scss'
})
export class SeatSelectionComponent implements OnInit, OnDestroy {
  seats: SelectableSeat[] = [];
  selectedSeats: SelectableSeat[] = [];
  loading = true;
  SeatStatus = PaymentServiceAgent.SeatStatus;
  showTimeId = '';
  roomId = '';

  private _subs = new Subscription();

  constructor(
    private _route: ActivatedRoute,
    private _router: Router,
    private _paymentService: PaymentServiceAgent.HttpService,
    private _hub: BookingHubService,
    private _cdr: ChangeDetectorRef,
  ) {}

  get rows(): string[] {
    return [...new Set(this.seats.map(s => s.rowName!))];
  }

  get totalPrice(): number {
    return this.selectedSeats.reduce((sum, s) => sum + (s.price ?? 0), 0);
  }

  getSeatsByRow(row: string): SelectableSeat[] {
    return this.seats.filter(s => s.rowName === row).sort((a, b) => (a.colIndex ?? 0) - (b.colIndex ?? 0));
  }

  ngOnInit(): void {
    this.showTimeId = this._route.snapshot.queryParams['showTimeId'] ?? '';
    this.roomId = this._route.snapshot.queryParams['roomId'] ?? '';
    this._paymentService.getSeats(
      PaymentServiceAgent.PagingSearchDTO.fromJS({ filters: { showTimeId: this.showTimeId, roomId: this.roomId } })
    ).subscribe({
      // Zoneless app: notify change detection so the seat grid renders.
      next: r => { this.seats = r.results ?? []; this.loading = false; this._cdr.markForCheck(); },
      error: () => { this.loading = false; this._cdr.markForCheck(); }
    });

    // Live seat locking: another viewer locking a seat disables it here in real time.
    this._subs.add(this._hub.seatLocked$.subscribe(e => {
      if (e.connectionId === this._hub.connectionId) return; // our own lock — ignore
      this._setLocked(e.seatId, true);
    }));
    this._subs.add(this._hub.seatUnlocked$.subscribe(seatId => this._setLocked(seatId, false)));
    // Our lock attempt lost the race — revert the optimistic selection.
    this._subs.add(this._hub.seatLockFailed$.subscribe(e => this._setLocked(e.seatId, true)));

    this._hub.startConnection(this.showTimeId, this.roomId).catch(() => { /* degrade to non-realtime */ });
  }

  toggleSeat(seat: SelectableSeat): void {
    // Double seats are two linked seats sharing a group id — select/lock them together.
    const group = this._groupOf(seat);
    if (group.some(s => s.status === PaymentServiceAgent.SeatStatus.Occupied || s.isLocked)) return;

    const select = !seat.isSelected;
    for (const s of group) {
      s.isSelected = select;
      if (select) {
        if (!this.selectedSeats.includes(s)) this.selectedSeats.push(s);
        this._hub.lockSeat(this.showTimeId, this.roomId, s.id!).catch(() => { /* see seatLockFailed$ */ });
      } else {
        this.selectedSeats = this.selectedSeats.filter(x => x.id !== s.id);
        this._hub.unlockSeat(this.showTimeId, this.roomId, s.id!).catch(() => {});
      }
    }
  }

  /** The seat plus any others sharing its group id (a double seat); just the seat itself otherwise. */
  private _groupOf(seat: SelectableSeat): SelectableSeat[] {
    if (!seat.seatGroupId) return [seat];
    return this.seats.filter(s => s.seatGroupId === seat.seatGroupId);
  }

  /** Apply a lock/unlock event to the matching seat, deselecting it if we held it. */
  private _setLocked(seatId: string, locked: boolean): void {
    const seat = this.seats.find(s => s.id === seatId);
    if (!seat) return;
    seat.isLocked = locked;
    if (locked && seat.isSelected) {
      seat.isSelected = false;
      this.selectedSeats = this.selectedSeats.filter(s => s.id !== seatId);
    }
    // SignalR event fires outside Angular — re-render the affected seat.
    this._cdr.markForCheck();
  }

  proceedToCheckout(): void {
    this._router.navigate(['/booking/confirmation'], {
      state: { showTimeId: this.showTimeId, roomId: this.roomId, seats: this.selectedSeats }
    });
  }

  ngOnDestroy(): void {
    // Note: selected seats are intentionally NOT unlocked here — when the user
    // proceeds to checkout we navigate away and the locks must survive into payment.
    this._subs.unsubscribe();
    this._hub.stopConnection();
  }
}

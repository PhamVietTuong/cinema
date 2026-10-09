import { ChangeDetectorRef, Component, DestroyRef, OnInit, inject, input } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormGroup } from '@angular/forms';
import { Store } from '@ngrx/store';
import { CinemaServiceAgent, SharedModule, showException } from 'CinemaLib';

/** Display label of a seat (row letter + column number). */
export function seatLabel(seat: { rowName?: string; colIndex?: number }): string {
  return `${seat.rowName ?? ''}${seat.colIndex ?? ''}`;
}

/**
 * Room (and optionally seat) selector for the incident dialogs. Binds to the `roomId` / `seatId` controls of the
 * parent form group; the seat list reloads whenever the room changes.
 */
@Component({
  selector: 'staff-room-seat-picker',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './room-seat-picker.component.html',
  styleUrl: './room-seat-picker.component.scss',
})
export class RoomSeatPickerComponent implements OnInit {
  readonly group = input.required<FormGroup>();
  readonly theaterId = input.required<string>();
  readonly showSeat = input<boolean>(true);

  rooms: CinemaServiceAgent.RoomDTO[] = [];
  seats: CinemaServiceAgent.RoomSeatDTO[] = [];

  private readonly _cinema = inject(CinemaServiceAgent.HttpService);
  private readonly _store = inject(Store);
  private readonly _cd = inject(ChangeDetectorRef);
  private readonly _destroyRef = inject(DestroyRef);

  label = seatLabel;

  ngOnInit(): void {
    this._cinema.getRooms(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: 1, pageSize: 100, filters: { theaterId: this.theaterId() },
    })).subscribe({
      next: result => {
        this.rooms = result.results ?? [];
        this._cd.markForCheck();
      },
      error: error => this._store.dispatch(showException({ error })),
    });

    const roomControl = this.group().get('roomId');
    roomControl?.valueChanges.pipe(takeUntilDestroyed(this._destroyRef)).subscribe(roomId => {
      this.group().get('seatId')?.setValue('');
      this._loadSeats(roomId);
    });
    this._loadSeats(roomControl?.value);
  }

  private _loadSeats(roomId: string | null | undefined): void {
    if (!roomId || !this.showSeat()) {
      this.seats = [];
      this._cd.markForCheck();
      return;
    }
    this._cinema.getRoomSeatMap(roomId).subscribe({
      next: seats => {
        this.seats = (seats ?? []).filter(seat => seat.isActive !== false);
        this._cd.markForCheck();
      },
      error: error => this._store.dispatch(showException({ error })),
    });
  }
}

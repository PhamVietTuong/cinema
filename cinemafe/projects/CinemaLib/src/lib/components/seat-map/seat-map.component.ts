import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { NgClass } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';
import { SelectableSeat, seatLabel, seatRows, seatsByRow, seatVisualState } from './seat-selection';

/**
 * Presentational seat grid: rows of seats with the screen arc and a legend. It owns no selection
 * logic; the screen mutates the seat flags (`isSelected`, `isSelectable`, `isAllowedForPatronCategory`,
 * `isLocked`, `status`) and re-renders the map by marking itself for check, then handles `seatToggle`.
 * Used by the customer booking page and the counter POS.
 *
 * Deliberately Default change detection: the seat objects are mutated in place, so an OnPush map
 * would never repaint.
 */
@Component({
  selector: 'cl-seat-map',
  standalone: true,
  imports: [NgClass, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.Default,
  templateUrl: './seat-map.component.html',
  styleUrl: './seat-map.component.scss',
})
export class SeatMapComponent {
  /** The seats of the room for the showtime; mutated in place by the owning screen. */
  @Input() seats: readonly SelectableSeat[] = [];
  /** False dims the grid and ignores clicks (nothing can be selected yet). */
  @Input() ready = true;
  /** Shows the "not available for this ticket type" legend entry. */
  @Input() showCategoryLegend = false;
  /** Shows the "held" legend entry (seats locked by another counter or customer). */
  @Input() showLockedLegend = false;
  /** Emits the clicked seat; the owner decides whether and how the selection changes. */
  @Output() seatToggle = new EventEmitter<SelectableSeat>();

  get rows(): string[] {
    return seatRows(this.seats);
  }

  seatsOf(row: string): SelectableSeat[] {
    return seatsByRow(this.seats, row);
  }

  label(seat: SelectableSeat): string {
    return seatLabel(seat);
  }

  state(seat: SelectableSeat): string {
    return seatVisualState(seat);
  }

  /** Tooltip i18n key (empty string = no tooltip). */
  titleOf(seat: SelectableSeat): string {
    if (seat.isAllowedForPatronCategory === false) {
      return 'seatMap.notAvailableForCategory';
    }
    if (seat.isSelectable === false) {
      return 'seatMap.capReached';
    }
    return '';
  }
}

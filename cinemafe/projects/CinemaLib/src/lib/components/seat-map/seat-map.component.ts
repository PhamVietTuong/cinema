import { ChangeDetectionStrategy, ChangeDetectorRef, Component, DoCheck, EventEmitter, Input, OnChanges, Output, inject } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { SelectableSeat, seatCellLabel, seatCellsByRow, seatCellVisualState } from './seat-selection';

/** One box of the seat grid with everything the template needs, computed once per seats change. */
export interface SeatCellView {
  key: string;
  isAisle: boolean;
  label: string;
  state: string;
  blocked: boolean;
  notSelectable: boolean;
  /** Tooltip i18n key (empty string = no tooltip). */
  titleKey: string;
  isDoubleSeat: boolean;
  isDoubleWide: boolean;
  /** The seat emitted on click (the first of a merged pair). */
  seat: SelectableSeat;
}

export interface SeatRowView {
  name: string;
  cells: SeatCellView[];
}

function tri(v: boolean | undefined): number {
  if (v === undefined) {
    return 0;
  }
  return v ? 1 : 2;
}

/** Packs the flags that affect drawing into one int (group id / col index only change with a new array). */
function seatCode(s: SelectableSeat): number {
  return (s.status ?? 0) | (tri(s.isSelected) << 2) | (tri(s.isSelectable) << 4)
    | (tri(s.isAllowedForPatronCategory) << 6) | (tri(s.isLocked) << 8) | (tri(s.isAisle) << 10)
    | (tri(s.isDouble) << 12);
}

/**
 * Presentational seat grid: rows of seats with the screen arc and a legend. It owns no selection
 * logic; the screen mutates the seat flags (`isSelected`, `isSelectable`, `isAllowedForPatronCategory`,
 * `isLocked`, `status`) — or replaces the `seats` array — and handles `seatToggle`.
 * Used by the customer booking page and the counter POS. A double (couple) seat is drawn as one wide
 * merged box covering its two underlying seats.
 *
 * OnPush with a precomputed view model: the rows/cells (label, state, blocked flags, tooltip) are built
 * once whenever the seats change, not per change-detection pass per cell. Because owners may still mutate
 * the seat objects in place, `ngDoCheck` fingerprints the seat flags with a cheap allocation-free pass
 * and rebuilds + marks for check only when something that affects drawing actually changed.
 */
@Component({
  selector: 'cl-seat-map',
  standalone: true,
  imports: [TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './seat-map.component.html',
  styleUrl: './seat-map.component.scss',
})
export class SeatMapComponent implements OnChanges, DoCheck {
  private readonly _cdr = inject(ChangeDetectorRef);

  /** The seats of the room for the showtime. */
  @Input() seats: readonly SelectableSeat[] = [];
  /** False dims the grid and ignores clicks (nothing can be selected yet). */
  @Input() ready = true;
  /** Shows the "not available for this ticket type" legend entry. */
  @Input() showCategoryLegend = false;
  /** Shows the "held" legend entry (seats locked by another counter or customer). */
  @Input() showLockedLegend = false;
  /** Emits the clicked seat; the owner decides whether and how the selection changes. */
  @Output() seatToggle = new EventEmitter<SelectableSeat>();

  rows: SeatRowView[] = [];

  private _fingerprint: Int32Array | null = null;

  ngOnChanges(): void {
    this.rebuild();
  }

  ngDoCheck(): void {
    const seats = this.seats;
    const fp = this._fingerprint;
    if (!fp || fp.length !== seats.length) {
      return;
    }
    for (let i = 0; i < seats.length; i++) {
      if (fp[i] !== seatCode(seats[i])) {
        this.rebuild();
        this._cdr.markForCheck();
        return;
      }
    }
  }

  private rebuild(): void {
    const seats = this.seats ?? [];
    const fp = new Int32Array(seats.length);
    const byRow = new Map<string, SelectableSeat[]>();
    for (let i = 0; i < seats.length; i++) {
      const s = seats[i];
      fp[i] = seatCode(s);
      const name = s.rowName ?? '';
      const list = byRow.get(name);
      if (list) {
        list.push(s);
      } else {
        byRow.set(name, [s]);
      }
    }
    this._fingerprint = fp;
    this.rows = [...byRow.entries()].map(([name, rowSeats]) => ({
      name,
      cells: seatCellsByRow(rowSeats, name).map(cell => {
        const blocked = cell.seats.some(s => s.isAllowedForPatronCategory === false);
        const anyNotSelectable = cell.seats.some(s => s.isSelectable === false);
        let titleKey = '';
        if (blocked) {
          titleKey = 'seatMap.notAvailableForCategory';
        } else if (anyNotSelectable) {
          titleKey = 'seatMap.capReached';
        }
        return {
          key: cell.key,
          isAisle: cell.isAisle,
          label: seatCellLabel(cell),
          state: seatCellVisualState(cell),
          blocked,
          notSelectable: !blocked && anyNotSelectable,
          titleKey,
          isDoubleSeat: !!cell.seats[0].isDouble,
          isDoubleWide: cell.isDouble,
          seat: cell.seats[0],
        };
      }),
    }));
  }

  onCellClick(cell: SeatCellView): void {
    if (cell.isAisle) {
      return;
    }
    this.seatToggle.emit(cell.seat);
  }
}

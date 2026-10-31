import { PaymentServiceAgent } from '../../services/payment-http.service';

/**
 * A seat as the seat map shows it: the API seat plus the client-side flags the owning screen keeps in
 * sync (the seat objects are mutated in place, the map only reads them).
 */
export type SelectableSeat = PaymentServiceAgent.SeatDTO & {
  /** This seat is part of the current selection. */
  isSelected?: boolean;
  /** False when the owning screen would currently refuse a click on it (e.g. every ticket already has a seat). */
  isSelectable?: boolean;
  /** False when the chosen ticket types can never use this kind of seat; drawn struck out. */
  isAllowedForPatronCategory?: boolean;
};

/** Visual state of a seat; the map derives it from the seat flags. */
export type SeatVisualState = 'available' | 'selected' | 'occupied' | 'locked' | 'unavailable-category';

/** Distinct row names in first-seen order. */
export function seatRows(seats: readonly SelectableSeat[]): string[] {
  return [...new Set(seats.map(s => s.rowName!))];
}

/** Minimal shape a seat needs to be grouped into rows and double-seat cells. */
export type RowedSeat = { id?: string; rowName?: string; colIndex?: number; seatGroupId?: string; displayNumber?: number; isAisle?: boolean };

/** The seats of one row ordered by column index. */
export function seatsByRow<T extends RowedSeat>(seats: readonly T[], row: string): T[] {
  return seats.filter(s => s.rowName === row).sort((a, b) => (a.colIndex ?? 0) - (b.colIndex ?? 0));
}

/** Short seat label such as "A12" — uses the server's row-position DisplayNumber (a double seat's two
 * halves share one number) when present, else falls back to the physical ColIndex. */
export function seatLabel(seat: Pick<RowedSeat, 'rowName' | 'colIndex' | 'displayNumber'>): string {
  return `${seat.rowName}${seat.displayNumber ?? seat.colIndex}`;
}

/** Removes duplicate labels (first-seen order), e.g. a double seat's two tickets both reading "C2". */
export function uniqueSeatLabels(labels: readonly string[]): string[] {
  return [...new Set(labels.filter(l => !!l))];
}

/** Dedupes and joins seat labels for a summary string, e.g. "C2, C3" rather than "C2, C2, C3". */
export function joinSeatLabels(labels: readonly string[], separator = ', '): string {
  return uniqueSeatLabels(labels).join(separator);
}

/**
 * The seat plus the others sharing its group id (a double seat is two linked seats); just the seat itself
 * when it has no group, or when the group is malformed (not exactly two seats).
 */
export function seatGroupOf(seats: readonly SelectableSeat[], seat: SelectableSeat): SelectableSeat[] {
  if (!seat.seatGroupId) {
    return [seat];
  }
  const group = seats.filter(s => s.seatGroupId === seat.seatGroupId);
  if (group.length !== 2) {
    console.warn(`Seat ${seat.id} has an invalid seatGroupId shared by ${group.length} seats; ignoring grouping.`);
    return [seat];
  }
  return group;
}

/** True when any seat of the group is sold or held by someone else, so the group cannot be picked. */
export function isGroupBlocked(group: readonly SelectableSeat[]): boolean {
  return group.some(s => s.status === PaymentServiceAgent.SeatStatus.Occupied || !!s.isLocked);
}

/**
 * Price attributed to one physical seat: a double seat's price covers the whole couple seat and is split
 * across its two halves so they sum to the single quoted price.
 */
export function physicalSeatPrice(unitPrice: number, isDouble: boolean | undefined): number {
  return isDouble ? unitPrice / 2 : unitPrice;
}

/** The state a seat is drawn in. Locked also covers a server-side Reserved seat. */
export function seatVisualState(seat: SelectableSeat): SeatVisualState {
  if (seat.isSelected) {
    return 'selected';
  }
  if (seat.status === PaymentServiceAgent.SeatStatus.Occupied) {
    return 'occupied';
  }
  if (seat.isLocked || seat.status === PaymentServiceAgent.SeatStatus.Reserved) {
    return 'locked';
  }
  if (seat.isAllowedForPatronCategory === false) {
    return 'unavailable-category';
  }
  return 'available';
}

/** One box the seat map draws: a single seat, or a merged pair for a double seat. */
export interface SeatCell<T extends RowedSeat = SelectableSeat> {
  key: string;
  seats: T[];
  isDouble: boolean;
  /** A non-bookable gap cell (aisle/walkway), rendered as empty space. Takes no position. */
  isAisle: boolean;
  /** 1-based position of this box within its row — a double seat counts as one position, so later
   * cells don't jump by two, and an aisle takes none at all. Only meaningful when seatCellsByRow saw
   * every seat of the row (as the admin seat-map editor does); the live booking/POS grid uses the
   * server's DisplayNumber instead. */
  position: number;
}

/**
 * The boxes to draw for one row: adjacent seats sharing a seatGroupId are merged into one wide cell.
 * A group that isn't exactly two adjacent seats in this row falls back to separate single-seat cells.
 * An aisle cell is never merged and never advances position. Generic so both the booking seat map
 * (SelectableSeat) and the admin seat-map editor (RoomSeatDTO) share it.
 */
export function seatCellsByRow<T extends RowedSeat>(seats: readonly T[], row: string): SeatCell<T>[] {
  const ordered = seatsByRow(seats, row);
  const groupCounts = new Map<string, number>();
  for (const s of ordered) {
    if (!s.isAisle && s.seatGroupId) {
      groupCounts.set(s.seatGroupId, (groupCounts.get(s.seatGroupId) ?? 0) + 1);
    }
  }

  const cells: SeatCell<T>[] = [];
  let position = 0;
  for (let i = 0; i < ordered.length; i++) {
    const seat = ordered[i];
    if (seat.isAisle) {
      cells.push({ key: `${seat.id}`, seats: [seat], isDouble: false, isAisle: true, position: 0 });
      continue;
    }

    const next = ordered[i + 1];
    const canMerge = !!seat.seatGroupId
      && !!next
      && !next.isAisle
      && next.seatGroupId === seat.seatGroupId
      && groupCounts.get(seat.seatGroupId) === 2;

    position++;
    if (canMerge) {
      cells.push({ key: `${seat.id}-${next.id}`, seats: [seat, next], isDouble: true, isAisle: false, position });
      i++;
    } else {
      cells.push({ key: `${seat.id}`, seats: [seat], isDouble: false, isAisle: false, position });
    }
  }
  return cells;
}

/** Label for a cell: a single seat's label, or a merged double seat's shared number (both halves read
 * the same, e.g. "C2") — relies on the seat carrying DisplayNumber (the live booking/POS grid). */
export function seatCellLabel(cell: SeatCell): string {
  return seatLabel(cell.seats[0]);
}

/** Visual state of a cell: the first state that applies across its seat(s), most urgent first. */
export function seatCellVisualState(cell: SeatCell): SeatVisualState {
  const states = cell.seats.map(seatVisualState);
  const priority: SeatVisualState[] = ['selected', 'occupied', 'locked', 'unavailable-category', 'available'];
  return priority.find(p => states.includes(p)) ?? 'available';
}

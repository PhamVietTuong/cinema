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

/** The seats of one row ordered by column index. */
export function seatsByRow(seats: readonly SelectableSeat[], row: string): SelectableSeat[] {
  return seats.filter(s => s.rowName === row).sort((a, b) => (a.colIndex ?? 0) - (b.colIndex ?? 0));
}

/** Short seat label such as "A12". */
export function seatLabel(seat: Pick<SelectableSeat, 'rowName' | 'colIndex'>): string {
  return `${seat.rowName}${seat.colIndex}`;
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

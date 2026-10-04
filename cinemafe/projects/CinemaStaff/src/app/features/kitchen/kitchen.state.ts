import { StaffServiceAgent } from 'CinemaLib';

type FoodOrderStatus = StaffServiceAgent.FoodOrderStatus;
const Status = StaffServiceAgent.FoodOrderStatus;

/** Board columns, left to right. HandedOver and Cancelled orders leave the board. */
export const QUEUE_COLUMNS: readonly FoodOrderStatus[] = [Status.Pending, Status.Preparing, Status.Ready];

/** Whether an order with this status belongs on the board. */
export function isOnBoard(status?: FoodOrderStatus): boolean {
  return status !== undefined && QUEUE_COLUMNS.includes(status);
}

/** The one step forward from a status (Pending to Preparing to Ready to HandedOver), or null at the end. */
export function nextFoodStatus(status?: FoodOrderStatus): FoodOrderStatus | null {
  switch (status) {
    case Status.Pending: return Status.Preparing;
    case Status.Preparing: return Status.Ready;
    case Status.Ready: return Status.HandedOver;
    default: return null;
  }
}

/** Mirror of the API rule: only the single next step is legal. */
export function isLegalTransition(from?: FoodOrderStatus, to?: FoodOrderStatus): boolean {
  const next = nextFoodStatus(from);
  return next !== null && next === to;
}

/** Orders of one column, earliest showtime first (the API's own order), then earliest payment. */
export function ordersInColumn(queue: readonly StaffServiceAgent.PickupOrderDTO[], status: FoodOrderStatus): StaffServiceAgent.PickupOrderDTO[] {
  return queue
    .filter(order => order.foodStatus === status)
    .sort((a, b) => time(a.showTimeStart) - time(b.showTimeStart) || time(a.paidAt) - time(b.paidAt));
}

/** Reducer for `FoodOrderQueued`: adds the order, or replaces it when already on the board. Off-board statuses are ignored. */
export function applyQueued(queue: readonly StaffServiceAgent.PickupOrderDTO[], order: StaffServiceAgent.PickupOrderDTO): StaffServiceAgent.PickupOrderDTO[] {
  const others = queue.filter(existing => existing.invoiceId !== order.invoiceId);
  return isOnBoard(order.foodStatus) ? [...others, order] : others;
}

/**
 * Reducer for `FoodOrderUpdated`: moves the card to its new column, or removes it when the order left the board.
 * An update for an order the board does not hold changes nothing; the caller refetches via `needsRefetch`.
 */
export function applyUpdated(
  queue: readonly StaffServiceAgent.PickupOrderDTO[],
  event: { invoiceId: string; foodStatus: FoodOrderStatus },
): StaffServiceAgent.PickupOrderDTO[] {
  if (!isOnBoard(event.foodStatus)) {
    return queue.filter(order => order.invoiceId !== event.invoiceId);
  }
  return queue.map(order => order.invoiceId === event.invoiceId ? cloneWithStatus(order, event.foodStatus) : order);
}

/** True when an event announces an on-board order the board has never seen (a missed `FoodOrderQueued`). */
export function needsRefetch(
  queue: readonly StaffServiceAgent.PickupOrderDTO[],
  event: { invoiceId: string; foodStatus: FoodOrderStatus },
): boolean {
  return isOnBoard(event.foodStatus) && !queue.some(order => order.invoiceId === event.invoiceId);
}

/** Whole minutes between `from` and `now`, never negative; 0 for a missing date. */
export function minutesSince(from: Date | undefined, now: Date): number {
  if (!from) {
    return 0;
  }
  return Math.max(0, Math.floor((now.getTime() - new Date(from).getTime()) / 60000));
}

function cloneWithStatus(order: StaffServiceAgent.PickupOrderDTO, status: FoodOrderStatus): StaffServiceAgent.PickupOrderDTO {
  const copy = StaffServiceAgent.PickupOrderDTO.fromJS(order.toJSON());
  copy.foodStatus = status;
  return copy;
}

function time(value?: Date): number {
  return value ? new Date(value).getTime() : Number.MAX_SAFE_INTEGER;
}

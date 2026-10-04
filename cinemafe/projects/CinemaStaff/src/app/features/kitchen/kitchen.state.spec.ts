import { StaffServiceAgent } from 'CinemaLib';
import {
  applyQueued,
  applyUpdated,
  isLegalTransition,
  isOnBoard,
  minutesSince,
  needsRefetch,
  nextFoodStatus,
  ordersInColumn,
} from './kitchen.state';

const Status = StaffServiceAgent.FoodOrderStatus;

function order(invoiceId: string, foodStatus: StaffServiceAgent.FoodOrderStatus, showTimeStart = '2026-10-04T18:00:00'): StaffServiceAgent.PickupOrderDTO {
  return StaffServiceAgent.PickupOrderDTO.fromJS({ invoiceId, invoiceCode: 'INV-' + invoiceId, foodStatus, showTimeStart, items: [] });
}

describe('food status transitions', () => {
  it('moves one step forward', () => {
    expect(nextFoodStatus(Status.Pending)).toBe(Status.Preparing);
    expect(nextFoodStatus(Status.Preparing)).toBe(Status.Ready);
    expect(nextFoodStatus(Status.Ready)).toBe(Status.HandedOver);
  });

  it('has no next step after hand-over, cancel or none', () => {
    expect(nextFoodStatus(Status.HandedOver)).toBeNull();
    expect(nextFoodStatus(Status.Cancelled)).toBeNull();
    expect(nextFoodStatus(Status.None)).toBeNull();
  });

  it('allows only the single next step', () => {
    expect(isLegalTransition(Status.Pending, Status.Preparing)).toBe(true);
    expect(isLegalTransition(Status.Pending, Status.Ready)).toBe(false);
    expect(isLegalTransition(Status.Ready, Status.Preparing)).toBe(false);
    expect(isLegalTransition(Status.HandedOver, Status.Ready)).toBe(false);
  });

  it('keeps only active statuses on the board', () => {
    expect(isOnBoard(Status.Ready)).toBe(true);
    expect(isOnBoard(Status.HandedOver)).toBe(false);
    expect(isOnBoard(Status.Cancelled)).toBe(false);
  });
});

describe('queue reducer', () => {
  it('adds a queued order and replaces a duplicate', () => {
    const queue = applyQueued([], order('a', Status.Pending));
    expect(queue.length).toBe(1);
    expect(applyQueued(queue, order('a', Status.Pending)).length).toBe(1);
  });

  it('ignores a queued order that is not on the board', () => {
    expect(applyQueued([], order('a', Status.None)).length).toBe(0);
  });

  it('moves a card to its new column on update', () => {
    const queue = applyUpdated([order('a', Status.Pending)], { invoiceId: 'a', foodStatus: Status.Preparing });
    expect(ordersInColumn(queue, Status.Pending).length).toBe(0);
    expect(ordersInColumn(queue, Status.Preparing).length).toBe(1);
  });

  it('removes a handed-over card', () => {
    const queue = applyUpdated([order('a', Status.Ready), order('b', Status.Ready)], { invoiceId: 'a', foodStatus: Status.HandedOver });
    expect(queue.map(o => o.invoiceId)).toEqual(['b']);
  });

  it('asks for a refetch only for an unknown on-board order', () => {
    const queue = [order('a', Status.Pending)];
    expect(needsRefetch(queue, { invoiceId: 'z', foodStatus: Status.Preparing })).toBe(true);
    expect(needsRefetch(queue, { invoiceId: 'a', foodStatus: Status.Preparing })).toBe(false);
    expect(needsRefetch(queue, { invoiceId: 'z', foodStatus: Status.HandedOver })).toBe(false);
  });

  it('sorts a column by showtime', () => {
    const queue = [order('late', Status.Pending, '2026-10-04T21:00:00'), order('early', Status.Pending, '2026-10-04T18:00:00')];
    expect(ordersInColumn(queue, Status.Pending).map(o => o.invoiceId)).toEqual(['early', 'late']);
  });
});

describe('minutesSince', () => {
  it('floors to whole minutes and never goes negative', () => {
    const now = new Date('2026-10-04T10:10:30');
    expect(minutesSince(new Date('2026-10-04T10:00:00'), now)).toBe(10);
    expect(minutesSince(new Date('2026-10-04T10:20:00'), now)).toBe(0);
    expect(minutesSince(undefined, now)).toBe(0);
  });
});

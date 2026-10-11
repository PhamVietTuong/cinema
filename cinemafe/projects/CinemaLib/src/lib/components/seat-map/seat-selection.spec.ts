import { PaymentServiceAgent } from '../../services/payment-http.service';
import { foodOrderCap } from '../../interfaces/cinema.model';
import {
  SelectableSeat, isGroupBlocked, joinSeatLabels, physicalSeatPrice, seatCellLabel, seatCellsByRow, seatCellVisualState,
  seatGroupOf, seatLabel, seatRows, seatsByRow, seatVisualState, uniqueSeatLabels,
} from './seat-selection';

const seat = (over: Partial<SelectableSeat>): SelectableSeat => ({
  id: 's1', rowName: 'A', colIndex: 1, isDouble: false, price: 100,
  status: PaymentServiceAgent.SeatStatus.Available, isLocked: false, ...over,
} as SelectableSeat);

describe('seat-selection helpers', () => {
  it('lists distinct rows and orders a row by column', () => {
    const seats = [seat({ id: 'a', colIndex: 3 }), seat({ id: 'b', colIndex: 1 }), seat({ id: 'c', rowName: 'B' })];
    expect(seatRows(seats)).toEqual(['A', 'B']);
    expect(seatsByRow(seats, 'A').map(s => s.id)).toEqual(['b', 'a']);
  });

  it('formats a seat label from ColIndex when there is no DisplayNumber', () => {
    expect(seatLabel(seat({ rowName: 'C', colIndex: 12 }))).toBe('C12');
  });

  it('prefers DisplayNumber over ColIndex when both are present', () => {
    expect(seatLabel(seat({ rowName: 'C', colIndex: 3, displayNumber: 2 }))).toBe('C2');
  });

  it('dedupes and joins seat labels, dropping empties', () => {
    expect(uniqueSeatLabels(['C2', 'C2', 'C3', ''])).toEqual(['C2', 'C3']);
    expect(joinSeatLabels(['C2', 'C2', 'C3'])).toBe('C2, C3');
    expect(joinSeatLabels(['C2', 'C3'], '-')).toBe('C2-C3');
  });

  it('groups the two halves of a double seat', () => {
    const a = seat({ id: 'a', seatGroupId: 'g' });
    const b = seat({ id: 'b', seatGroupId: 'g' });
    const c = seat({ id: 'c' });
    expect(seatGroupOf([a, b, c], a).map(s => s.id)).toEqual(['a', 'b']);
    expect(seatGroupOf([a, b, c], c)).toEqual([c]);
  });

  it('ignores a malformed group (not exactly two seats)', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const a = seat({ id: 'a', seatGroupId: 'g' });
    expect(seatGroupOf([a], a)).toEqual([a]);
    warn.mockRestore();
  });

  it('blocks a group when any half is sold or held', () => {
    expect(isGroupBlocked([seat({}), seat({ isLocked: true })])).toBe(true);
    expect(isGroupBlocked([seat({ status: PaymentServiceAgent.SeatStatus.Occupied })])).toBe(true);
    expect(isGroupBlocked([seat({}), seat({})])).toBe(false);
  });

  it('splits a double seat price across its halves', () => {
    expect(physicalSeatPrice(120000, true)).toBe(60000);
    expect(physicalSeatPrice(80000, false)).toBe(80000);
  });

  it('derives the visual state', () => {
    expect(seatVisualState(seat({}))).toBe('available');
    expect(seatVisualState(seat({ isSelected: true }))).toBe('selected');
    expect(seatVisualState(seat({ status: PaymentServiceAgent.SeatStatus.Occupied }))).toBe('occupied');
    expect(seatVisualState(seat({ isLocked: true }))).toBe('locked');
    expect(seatVisualState(seat({ status: PaymentServiceAgent.SeatStatus.Reserved }))).toBe('locked');
    expect(seatVisualState(seat({ isAllowedForPatronCategory: false }))).toBe('unavailable-category');
  });

  it('merges an adjacent double-seat pair into one cell, leaving singles alone', () => {
    const one = seat({ id: '1', colIndex: 1 });
    const two = seat({ id: '2', colIndex: 2, seatGroupId: 'g', isDouble: true });
    const three = seat({ id: '3', colIndex: 3, seatGroupId: 'g', isDouble: true });
    const four = seat({ id: '4', colIndex: 4 });
    const cells = seatCellsByRow([four, three, one, two], 'A');
    expect(cells).toEqual([
      { key: '1', seats: [one], isDouble: false, position: 1 },
      { key: '2-3', seats: [two, three], isDouble: true, position: 2 },
      { key: '4', seats: [four], isDouble: false, position: 3 },
    ]);
  });

  it('falls back to single cells for a group that is not exactly two adjacent seats in the row', () => {
    const a = seat({ id: 'a', colIndex: 1, seatGroupId: 'g' });
    const b = seat({ id: 'b', colIndex: 3, seatGroupId: 'g' });
    const middle = seat({ id: 'm', colIndex: 2 });
    expect(seatCellsByRow([a, middle, b], 'A').map(c => ({ key: c.key, position: c.position })))
      .toEqual([{ key: 'a', position: 1 }, { key: 'm', position: 2 }, { key: 'b', position: 3 }]);

    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const c = seat({ id: 'c', colIndex: 1, seatGroupId: 'h' });
    const d = seat({ id: 'd', colIndex: 2, seatGroupId: 'h' });
    const e = seat({ id: 'e', colIndex: 3, seatGroupId: 'h' });
    expect(seatCellsByRow([c, d, e], 'A').map(cell => cell.key)).toEqual(['c', 'd', 'e']);
    warn.mockRestore();
  });

  it('labels a cell from its first seat — a merged double seat shows one shared number', () => {
    const one = seat({ id: '1', rowName: 'N', colIndex: 1, displayNumber: 1 });
    const two = seat({ id: '2', rowName: 'N', colIndex: 2, displayNumber: 2 });
    const three = seat({ id: '3', rowName: 'N', colIndex: 3, displayNumber: 2 });
    expect(seatCellLabel({ key: '1', seats: [one], isDouble: false, position: 1 })).toBe('N1');
    expect(seatCellLabel({ key: '2-3', seats: [two, three], isDouble: true, position: 2 })).toBe('N2');
  });

  it('derives a cell visual state from the most urgent seat state in it', () => {
    const avail = seat({ id: 'a' });
    const sel = seat({ id: 'b', isSelected: true });
    const occ = seat({ id: 'c', status: PaymentServiceAgent.SeatStatus.Occupied });
    const locked = seat({ id: 'd', isLocked: true });
    const blocked = seat({ id: 'e', isAllowedForPatronCategory: false });

    expect(seatCellVisualState({ key: 'x', seats: [avail, avail], isDouble: true, position: 1 })).toBe('available');
    expect(seatCellVisualState({ key: 'x', seats: [avail, sel], isDouble: true, position: 1 })).toBe('selected');
    expect(seatCellVisualState({ key: 'x', seats: [avail, occ], isDouble: true, position: 1 })).toBe('occupied');
    expect(seatCellVisualState({ key: 'x', seats: [avail, locked], isDouble: true, position: 1 })).toBe('locked');
    expect(seatCellVisualState({ key: 'x', seats: [avail, blocked], isDouble: true, position: 1 })).toBe('unavailable-category');
  });
});

describe('foodOrderCap', () => {
  it('is 0 when sold out, the tracked quantity when limited, null when unlimited', () => {
    expect(foodOrderCap({ isOutOfStock: true, availableQuantity: 5 })).toBe(0);
    expect(foodOrderCap({ availableQuantity: 4 })).toBe(4);
    expect(foodOrderCap({ availableQuantity: -2 })).toBe(0);
    expect(foodOrderCap({})).toBeNull();
  });
});

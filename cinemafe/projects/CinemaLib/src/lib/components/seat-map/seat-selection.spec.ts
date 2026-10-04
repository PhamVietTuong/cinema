import { PaymentServiceAgent } from '../../services/payment-http.service';
import { foodOrderCap } from '../../interfaces/cinema.model';
import {
  SelectableSeat, isGroupBlocked, physicalSeatPrice, seatGroupOf, seatLabel, seatRows, seatsByRow, seatVisualState,
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

  it('formats a seat label', () => {
    expect(seatLabel(seat({ rowName: 'C', colIndex: 12 }))).toBe('C12');
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
});

describe('foodOrderCap', () => {
  it('is 0 when sold out, the tracked quantity when limited, null when unlimited', () => {
    expect(foodOrderCap({ isOutOfStock: true, availableQuantity: 5 })).toBe(0);
    expect(foodOrderCap({ availableQuantity: 4 })).toBe(4);
    expect(foodOrderCap({ availableQuantity: -2 })).toBe(0);
    expect(foodOrderCap({})).toBeNull();
  });
});

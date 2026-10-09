import { CinemaServiceAgent } from 'CinemaLib';
import {
  PosFoodLine, PosTicket, TenderEntry, defaultPriceRow, foodItems, foodsTotal, hasOverride, remainingAfter, seatItems, selectionTotal, settle, ticketPrice, ticketsTotal,
} from './pos-calc';

const T = CinemaServiceAgent.PaymentTender;
const tender = (method: CinemaServiceAgent.PaymentTender, amount: number, reference = ''): TenderEntry => ({ method, amount, reference });
const rows = [{ patronCategoryId: 'adult', price: 100000 }, { patronCategoryId: 'couple', price: 220000 }];
const single: PosTicket = { seatIds: ['a1'], label: 'A1', isDouble: false, patronCategoryId: 'adult' };
const couple: PosTicket = { seatIds: ['b1', 'b2'], label: 'B1-B2', isDouble: true, patronCategoryId: 'couple' };
const combo: PosFoodLine = { foodAndDrinkId: 'f1', name: 'Combo', unitPrice: 90000, quantity: 2 };

describe('selection totals', () => {
  it('prices a ticket from its category row and a double ticket once', () => {
    expect(ticketPrice(single, rows)).toBe(100000);
    expect(ticketsTotal([single, couple], rows)).toBe(320000);
  });

  it('an override replaces the list price', () => {
    expect(ticketPrice({ ...single, overridePrice: 50000 }, rows)).toBe(50000);
    expect(foodsTotal([{ ...combo, overridePrice: 80000 }])).toBe(160000);
  });

  it('adds tickets and food (a combo is one line)', () => {
    expect(foodsTotal([combo])).toBe(180000);
    expect(selectionTotal([single], rows, [combo])).toBe(280000);
    expect(selectionTotal([], rows, [combo])).toBe(180000);
  });

  it('splits a double ticket override across its two seats and drops empty food lines', () => {
    const items = seatItems([{ ...couple, overridePrice: 200000 }, single]);
    expect(items).toEqual([
      { seatId: 'b1', patronCategoryId: 'couple', overrideUnitPrice: 100000 },
      { seatId: 'b2', patronCategoryId: 'couple', overrideUnitPrice: 100000 },
      { seatId: 'a1', patronCategoryId: 'adult', overrideUnitPrice: undefined },
    ]);
    expect(foodItems([combo, { ...combo, quantity: 0 }])).toHaveLength(1);
  });

  it('detects overrides', () => {
    expect(hasOverride([single], [combo])).toBe(false);
    expect(hasOverride([{ ...single, overridePrice: 1 }], [])).toBe(true);
    expect(hasOverride([], [{ ...combo, overridePrice: 1 }])).toBe(true);
  });
});

describe('tender settlement', () => {
  it('cash paid exactly settles with no change', () => {
    const s = settle(280000, [tender(T.Cash, 280000)]);
    expect(s.complete).toBe(true);
    expect(s.changeDue).toBe(0);
    expect(s.usesCash).toBe(true);
  });

  it('cash over the total returns change', () => {
    const s = settle(280000, [tender(T.Cash, 300000)]);
    expect(s.complete).toBe(true);
    expect(s.changeDue).toBe(20000);
  });

  it('a short payment is incomplete and reports the remainder', () => {
    const s = settle(280000, [tender(T.Cash, 100000)]);
    expect(s.complete).toBe(false);
    expect(s.remaining).toBe(180000);
  });

  it('splits 50/50 between cash and card with a reference', () => {
    const s = settle(200000, [tender(T.Cash, 100000), tender(T.Card, 100000, 'RRN123')]);
    expect(s.complete).toBe(true);
    expect(s.changeDue).toBe(0);
  });

  it('card and QR lines need a reference', () => {
    expect(settle(100000, [tender(T.Card, 100000)]).missingReference).toBe(true);
    expect(settle(100000, [tender(T.QrWallet, 100000, '  ')]).complete).toBe(false);
    expect(settle(100000, [tender(T.QrWallet, 100000, 'TX1')]).complete).toBe(true);
  });

  it('non-cash over the due amount is refused, not turned into change', () => {
    const s = settle(100000, [tender(T.Card, 150000, 'R')]);
    expect(s.nonCashExcess).toBe(true);
    expect(s.complete).toBe(false);
    expect(s.changeDue).toBe(0);
  });

  it('change comes only out of cash when mixed with card', () => {
    const s = settle(100000, [tender(T.Card, 60000, 'R'), tender(T.Cash, 50000)]);
    expect(s.complete).toBe(true);
    expect(s.changeDue).toBe(10000);
  });

  it('a zero-amount line is invalid and nothing due needs no tender', () => {
    expect(settle(100000, [tender(T.Cash, 0)]).invalidAmount).toBe(true);
    const free = settle(0, []);
    expect(free.complete).toBe(true);
    expect(free.usesCash).toBe(false);
  });

  it('computes the remainder to prefill a line with', () => {
    const lines = [tender(T.Cash, 120000), tender(T.Card, 0, '')];
    expect(remainingAfter(200000, lines, 1)).toBe(80000);
    expect(remainingAfter(200000, lines, 0)).toBe(200000);
  });
});

describe('defaultPriceRow', () => {
  it('picks the dearest category, not the first by name', () => {
    const list = [
      { patronCategoryId: 'student', patronCategoryName: 'Học Sinh', price: 65000 },
      { patronCategoryId: 'adult', patronCategoryName: 'Người Lớn', price: 90000 },
      { patronCategoryId: 'child', patronCategoryName: 'Trẻ Em', price: 50000 },
    ];
    expect(defaultPriceRow(list)?.patronCategoryId).toBe('adult');
  });

  it('breaks price ties by name and tolerates an empty list', () => {
    expect(defaultPriceRow([{ patronCategoryName: 'B', price: 1 }, { patronCategoryName: 'A', price: 1 }])?.patronCategoryName).toBe('A');
    expect(defaultPriceRow([])).toBeUndefined();
  });
});

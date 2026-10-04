import { StaffServiceAgent } from 'CinemaLib';
import { TenderEntry } from '../../core/pos-calc';
import { ExchangeSubmitState, exchangeDifference, exchangeNeedsDrawer, exchangeSettlement, exchangeSubmitErrors } from './exchange.logic';

const Tender = StaffServiceAgent.PaymentTender;

const cash = (amount: number): TenderEntry => ({ method: Tender.Cash, amount, reference: '' });
const card = (amount: number, reference = ''): TenderEntry => ({ method: Tender.Card, amount, reference });

function state(over: Partial<ExchangeSubmitState> = {}): ExchangeSubmitState {
  const diff = over.diff ?? exchangeDifference(100_000, 150_000);
  return {
    hasQuote: true, quoting: false, submitting: false, hasItems: true,
    diff,
    settlement: exchangeSettlement(diff, [cash(50_000)]),
    drawerOpen: true,
    refundTender: Tender.Cash,
    ...over,
  };
}

describe('exchangeDifference', () => {
  it('collects the surplus of a dearer replacement', () => {
    expect(exchangeDifference(100_000, 150_000)).toEqual({ difference: 50_000, collect: 50_000, payBack: 0 });
  });

  it('pays back the surplus of a cheaper replacement', () => {
    expect(exchangeDifference(150_000, 100_000)).toEqual({ difference: -50_000, collect: 0, payBack: 50_000 });
  });

  it('moves nothing for an even exchange', () => {
    expect(exchangeDifference(120_000, 120_000)).toEqual({ difference: 0, collect: 0, payBack: 0 });
  });

  it('rounds to whole dong', () => {
    expect(exchangeDifference(100_000.4, 100_999.6).difference).toBe(1000);
  });
});

describe('exchangeSettlement', () => {
  it('settles only the difference over the old invoice', () => {
    const diff = exchangeDifference(100_000, 150_000);
    const s = exchangeSettlement(diff, [cash(60_000)]);
    expect(s.due).toBe(50_000);
    expect(s.changeDue).toBe(10_000);
    expect(s.complete).toBe(true);
  });

  it('is incomplete until the difference is covered', () => {
    const s = exchangeSettlement(exchangeDifference(100_000, 150_000), [cash(20_000)]);
    expect(s.remaining).toBe(30_000);
    expect(s.complete).toBe(false);
  });

  it('ignores tenders when the replacement is not dearer', () => {
    const s = exchangeSettlement(exchangeDifference(150_000, 100_000), [cash(10_000)]);
    expect(s.due).toBe(0);
    expect(s.complete).toBe(true);
    expect(s.usesCash).toBe(false);
  });
});

describe('exchangeNeedsDrawer', () => {
  it('needs a drawer to take cash', () => {
    const diff = exchangeDifference(100_000, 150_000);
    expect(exchangeNeedsDrawer(diff, exchangeSettlement(diff, [cash(50_000)]), Tender.Card)).toBe(true);
  });

  it('needs a drawer to pay back in cash but not by card', () => {
    const diff = exchangeDifference(150_000, 100_000);
    const s = exchangeSettlement(diff, []);
    expect(exchangeNeedsDrawer(diff, s, Tender.Cash)).toBe(true);
    expect(exchangeNeedsDrawer(diff, s, Tender.Card)).toBe(false);
  });

  it('needs none for a card-settled difference', () => {
    const diff = exchangeDifference(100_000, 150_000);
    expect(exchangeNeedsDrawer(diff, exchangeSettlement(diff, [card(50_000, 'R1')]), Tender.Cash)).toBe(false);
  });
});

describe('exchangeSubmitErrors', () => {
  it('is empty when everything is in place', () => {
    expect(exchangeSubmitErrors(state())).toEqual([]);
  });

  it('asks for a cart first', () => {
    expect(exchangeSubmitErrors(state({ hasItems: false, hasQuote: false }))).toEqual(['afterSales.exchange.error.noItems']);
  });

  it('waits for the quote', () => {
    expect(exchangeSubmitErrors(state({ quoting: true }))).toContain('afterSales.exchange.error.noQuote');
  });

  it('blocks while the difference is not covered', () => {
    const diff = exchangeDifference(100_000, 150_000);
    expect(exchangeSubmitErrors(state({ diff, settlement: exchangeSettlement(diff, [cash(10_000)]) })))
      .toContain('afterSales.exchange.error.tendersIncomplete');
  });

  it('blocks a cash step with a closed drawer', () => {
    expect(exchangeSubmitErrors(state({ drawerOpen: false }))).toContain('afterSales.exchange.error.drawer');
  });

  it('lets a cheaper replacement through with a card pay-back and no drawer', () => {
    const diff = exchangeDifference(150_000, 100_000);
    expect(exchangeSubmitErrors(state({ diff, settlement: exchangeSettlement(diff, []), drawerOpen: false, refundTender: Tender.Card }))).toEqual([]);
  });
});

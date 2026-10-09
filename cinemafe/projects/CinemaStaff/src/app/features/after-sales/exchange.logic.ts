import { StaffServiceAgent } from 'CinemaLib';
import { Settlement, TenderEntry, settle } from '../../core/pos-calc';

/** What changes hands in an exchange: the new total against the final amount of the invoice being replaced. */
export interface ExchangeDifference {
  /** New final amount minus the old one (negative = the replacement is cheaper). */
  difference: number;
  /** The customer still owes this much; the tenders settle only this. */
  collect: number;
  /** The customer gets this much back through the refund tender. */
  payBack: number;
}

const round = (n: number): number => Math.round(n);

/** With nothing picked yet (`hasNewItems` false) there is no difference to show, instead of a refund of the whole old amount. */
export function exchangeDifference(oldFinalAmount: number, newFinalAmount: number, hasNewItems = true): ExchangeDifference {
  if (!hasNewItems) {
    return { difference: 0, collect: 0, payBack: 0 };
  }
  const difference = round(newFinalAmount) - round(oldFinalAmount);
  return { difference, collect: Math.max(difference, 0), payBack: Math.max(-difference, 0) };
}

/** Tenders settle only what is collected over the old invoice; with nothing to collect there is nothing to tender. */
export function exchangeSettlement(diff: ExchangeDifference, tenders: readonly TenderEntry[]): Settlement {
  return settle(diff.collect, diff.collect > 0 ? tenders : []);
}

/** A cash drawer is needed to take cash or to pay a cheaper replacement back in cash. */
export function exchangeNeedsDrawer(diff: ExchangeDifference, settlement: Settlement, refundTender: StaffServiceAgent.PaymentTender): boolean {
  return settlement.usesCash || (diff.payBack > 0 && refundTender === StaffServiceAgent.PaymentTender.Cash);
}

export interface ExchangeSubmitState {
  hasQuote: boolean;
  quoting: boolean;
  submitting: boolean;
  hasItems: boolean;
  diff: ExchangeDifference;
  settlement: Settlement;
  drawerOpen: boolean;
  refundTender: StaffServiceAgent.PaymentTender;
}

/** i18n keys (under `afterSales.exchange.error`) of everything still blocking the Confirm button; empty means ready. */
export function exchangeSubmitErrors(s: ExchangeSubmitState): string[] {
  const errors: string[] = [];
  if (!s.hasItems) {
    errors.push('afterSales.exchange.error.noItems');
  } else if (!s.hasQuote || s.quoting) {
    errors.push('afterSales.exchange.error.noQuote');
  }
  if (s.diff.collect > 0 && !s.settlement.complete) {
    errors.push('afterSales.exchange.error.tendersIncomplete');
  }
  if (exchangeNeedsDrawer(s.diff, s.settlement, s.refundTender) && !s.drawerOpen) {
    errors.push('afterSales.exchange.error.drawer');
  }
  if (s.submitting) {
    errors.push('afterSales.exchange.error.busy');
  }
  return errors;
}

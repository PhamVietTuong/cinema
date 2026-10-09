import { APPROVER_ROLES, CinemaServiceAgent } from 'CinemaLib';

export { isOverrideRejection } from '../../core/sensitive-call.service';

/** A manager PIN is needed unless the signed-in user already holds an approver role. */
export function needsManagerOverride(role: string | null | undefined): boolean {
  return !role || !APPROVER_ROLES.includes(role);
}

/** Search needs at least one non-blank criterion. */
export function hasSearchCriteria(code: string | null | undefined, phone: string | null | undefined): boolean {
  return !!(code ?? '').trim() || !!(phone ?? '').trim();
}

export interface RefundFormValue {
  reasonCode: CinemaServiceAgent.StaffReasonCode | null | undefined;
  note: string | null | undefined;
  tender: CinemaServiceAgent.PaymentTender | null | undefined;
  reference: string | null | undefined;
}

/** Rules for the refund tender part of the form (the panel checks them before the reason dialog opens). */
export function refundTenderErrors(
  tender: CinemaServiceAgent.PaymentTender | null | undefined,
  reference: string | null | undefined,
): string[] {
  if (tender === null || tender === undefined) {
    return ['afterSales.refund.error.tenderRequired'];
  }
  if (tender !== CinemaServiceAgent.PaymentTender.Cash && !(reference ?? '').trim()) {
    return ['afterSales.refund.error.referenceRequired'];
  }
  return [];
}

/** Rules for the reason part: a code is required and reason Other needs a note. */
export function refundReasonErrors(
  reasonCode: CinemaServiceAgent.StaffReasonCode | null | undefined,
  note: string | null | undefined,
): string[] {
  if (reasonCode === null || reasonCode === undefined) {
    return ['afterSales.refund.error.reasonRequired'];
  }
  if (reasonCode === CinemaServiceAgent.StaffReasonCode.Other && !(note ?? '').trim()) {
    return ['afterSales.refund.error.noteRequired'];
  }
  return [];
}

/** i18n keys (under `afterSales.refund.error`) of every rule the refund form currently breaks; empty means valid. */
export function refundFormErrors(value: RefundFormValue): string[] {
  return [
    ...refundReasonErrors(value.reasonCode, value.note),
    ...refundTenderErrors(value.tender, value.reference),
  ];
}

/** Reprint reasons are free text, at least 3 characters once trimmed. */
export function isValidReprintReason(reason: string | null | undefined): boolean {
  return (reason ?? '').trim().length >= 3;
}

/** Only counter sales that are paid, unrefunded and have no used ticket can be exchanged. */
export function canExchangeInvoice(invoice: CinemaServiceAgent.AfterSalesInvoiceDTO): boolean {
  return invoice.channel === CinemaServiceAgent.SalesChannel.Counter
    && invoice.status === CinemaServiceAgent.InvoiceStatus.Paid
    && (invoice.usedTicketCount ?? 0) === 0
    && !!invoice.canRefund;
}

/** Only paid invoices have tickets worth reprinting. */
export function canReprintInvoice(invoice: CinemaServiceAgent.AfterSalesInvoiceDTO): boolean {
  return invoice.status === CinemaServiceAgent.InvoiceStatus.Paid;
}

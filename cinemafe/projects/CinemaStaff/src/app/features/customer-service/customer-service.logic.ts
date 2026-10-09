import { StaffServiceAgent } from 'CinemaLib';

type Status = StaffServiceAgent.ComplaintStatus;
type Resolution = StaffServiceAgent.ComplaintResolution;
type Channel = StaffServiceAgent.ETicketChannel;
type Tender = StaffServiceAgent.PaymentTender;

// ── E-ticket resend limiter ─────────────────────────────────────────────────────────────────────────

/** The API allows this many resends per invoice per rolling hour (a 4th gets a 400). */
export const MAX_RESENDS_PER_HOUR = 3;
const HOUR_MS = 60 * 60 * 1000;

/** What the last resend of an invoice reported: the API's `remainingThisHour` and when it answered. */
export interface ResendQuota {
  remaining: number;
  at: number;
}

/**
 * Resends still available for an invoice. Without a recent answer (none yet, or older than an hour) the full allowance
 * is assumed; the API stays the judge, this only drives the hint and disables the button when it knows none are left.
 */
export function resendRemaining(quota: ResendQuota | undefined, now: number): number {
  if (!quota || now - quota.at >= HOUR_MS) {
    return MAX_RESENDS_PER_HOUR;
  }
  return Math.max(0, Math.min(MAX_RESENDS_PER_HOUR, quota.remaining));
}

export function resendExhausted(quota: ResendQuota | undefined, now: number): boolean {
  return resendRemaining(quota, now) === 0;
}

const EMAIL = /^\S+@\S+\.\S+$/;
const PHONE = /^\+?[0-9 ()-]{8,}$/;

/** An address typed for the resend is optional (the contact on file is used), but must look right for the channel. */
export function resendAddressErrors(channel: Channel | null | undefined, address: string | null | undefined): string[] {
  const value = (address ?? '').trim();
  if (!value) {
    return [];
  }
  if (channel === StaffServiceAgent.ETicketChannel.Sms) {
    return PHONE.test(value) ? [] : ['customerService.resend.error.phone'];
  }
  return EMAIL.test(value) ? [] : ['customerService.resend.error.email'];
}

// ── Complaint workflow ──────────────────────────────────────────────────────────────────────────────

export type ComplaintAction = 'edit' | 'startReview' | 'reject' | 'resolve';

/** Open -> InReview -> Resolved | Rejected; an open or in-review complaint can also be rejected / resolved directly or edited. */
export function complaintActions(status: Status | undefined): ComplaintAction[] {
  switch (status) {
    case StaffServiceAgent.ComplaintStatus.Open:
      return ['edit', 'startReview', 'reject', 'resolve'];
    case StaffServiceAgent.ComplaintStatus.InReview:
      return ['edit', 'reject', 'resolve'];
    default:
      return [];
  }
}

export function isComplaintClosed(status: Status | undefined): boolean {
  return status === StaffServiceAgent.ComplaintStatus.Resolved || status === StaffServiceAgent.ComplaintStatus.Rejected;
}

/** Everything but an apology is a compensation that needs an approver or a manager PIN override. */
export function resolutionNeedsApproval(resolution: Resolution | null | undefined): boolean {
  return resolution === StaffServiceAgent.ComplaintResolution.Refund
    || resolution === StaffServiceAgent.ComplaintResolution.GiftCard
    || resolution === StaffServiceAgent.ComplaintResolution.Points;
}

/** The PIN prompt opens first unless the user is an approver or the resolution is an apology. */
export function resolutionNeedsPin(resolution: Resolution | null | undefined, isApprover: boolean): boolean {
  return resolutionNeedsApproval(resolution) && !isApprover;
}

/** Resolutions the complaint's links allow: a refund needs an invoice, a gift card or points need the customer. */
export function availableResolutions(link: { hasInvoice: boolean; hasCustomer: boolean }): Resolution[] {
  const result: Resolution[] = [];
  if (link.hasInvoice) {
    result.push(StaffServiceAgent.ComplaintResolution.Refund);
  }
  if (link.hasCustomer) {
    result.push(StaffServiceAgent.ComplaintResolution.GiftCard, StaffServiceAgent.ComplaintResolution.Points);
  }
  result.push(StaffServiceAgent.ComplaintResolution.Apology);
  return result;
}

export interface ResolveForm {
  resolution: Resolution | null | undefined;
  amount: number | null | undefined;
  refundTender: Tender | null | undefined;
  refundReference: string | null | undefined;
  hasInvoice: boolean;
  hasCustomer: boolean;
}

/** i18n keys (under `customerService.resolve.error`) of every rule the compensation form breaks; empty means valid. */
export function resolveErrors(form: ResolveForm): string[] {
  const errors: string[] = [];
  const resolution = form.resolution;
  if (resolution === null || resolution === undefined || resolution === StaffServiceAgent.ComplaintResolution.None
    || !availableResolutions(form).includes(resolution)) {
    return ['customerService.resolve.error.resolution'];
  }
  if (resolution === StaffServiceAgent.ComplaintResolution.GiftCard || resolution === StaffServiceAgent.ComplaintResolution.Points) {
    const amount = form.amount ?? 0;
    if (!(amount > 0)) {
      errors.push('customerService.resolve.error.amount');
    } else if (resolution === StaffServiceAgent.ComplaintResolution.Points && amount !== Math.floor(amount)) {
      errors.push('customerService.resolve.error.pointsWhole');
    }
  }
  if (resolution === StaffServiceAgent.ComplaintResolution.Refund) {
    if (form.refundTender === null || form.refundTender === undefined) {
      errors.push('customerService.resolve.error.tender');
    } else if (form.refundTender !== StaffServiceAgent.PaymentTender.Cash && !(form.refundReference ?? '').trim()) {
      errors.push('customerService.resolve.error.reference');
    }
  }
  return errors;
}

/** A complaint needs a description of at least 3 characters once trimmed. */
export function isValidComplaintDescription(description: string | null | undefined): boolean {
  return (description ?? '').trim().length >= 3;
}

/** Complaints list filters the API understands; blank values are dropped. */
export function complaintFilters(raw: Record<string, unknown>, theaterId?: string | null): Record<string, string> {
  const filters: Record<string, string> = {};
  if (theaterId) {
    filters['theaterId'] = theaterId;
  }
  for (const key of Object.keys(raw)) {
    const value = (raw[key] ?? '').toString().trim();
    if (value) {
      filters[key] = value;
    }
  }
  return filters;
}

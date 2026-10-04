import { StaffServiceAgent } from 'CinemaLib';
import {
  MAX_RESENDS_PER_HOUR, ResolveForm, availableResolutions, complaintActions, complaintFilters, isComplaintClosed, isValidComplaintDescription,
  resendAddressErrors, resendExhausted, resendRemaining, resolutionNeedsApproval, resolutionNeedsPin, resolveErrors,
} from './customer-service.logic';

const Status = StaffServiceAgent.ComplaintStatus;
const Resolution = StaffServiceAgent.ComplaintResolution;
const Tender = StaffServiceAgent.PaymentTender;
const HOUR = 60 * 60 * 1000;

function form(over: Partial<ResolveForm> = {}): ResolveForm {
  return { resolution: Resolution.Apology, amount: null, refundTender: Tender.Cash, refundReference: '', hasInvoice: true, hasCustomer: true, ...over };
}

describe('resend limiter', () => {
  it('assumes the full allowance with no recent answer', () => {
    expect(resendRemaining(undefined, 1000)).toBe(MAX_RESENDS_PER_HOUR);
    expect(resendRemaining({ remaining: 0, at: 0 }, HOUR)).toBe(MAX_RESENDS_PER_HOUR);
  });

  it('shows what the last resend reported within the hour', () => {
    expect(resendRemaining({ remaining: 2, at: 1000 }, 5000)).toBe(2);
    expect(resendRemaining({ remaining: 7, at: 1000 }, 5000)).toBe(MAX_RESENDS_PER_HOUR);
  });

  it('is exhausted at zero until the hour passes', () => {
    expect(resendExhausted({ remaining: 0, at: 0 }, HOUR - 1)).toBe(true);
    expect(resendExhausted({ remaining: 0, at: 0 }, HOUR)).toBe(false);
    expect(resendExhausted({ remaining: 1, at: 0 }, 1)).toBe(false);
  });

  it('accepts a blank address and checks a typed one for its channel', () => {
    expect(resendAddressErrors(StaffServiceAgent.ETicketChannel.Email, '  ')).toEqual([]);
    expect(resendAddressErrors(StaffServiceAgent.ETicketChannel.Email, 'a@b.vn')).toEqual([]);
    expect(resendAddressErrors(StaffServiceAgent.ETicketChannel.Email, '0901234567')).toEqual(['customerService.resend.error.email']);
    expect(resendAddressErrors(StaffServiceAgent.ETicketChannel.Sms, '0901234567')).toEqual([]);
    expect(resendAddressErrors(StaffServiceAgent.ETicketChannel.Sms, 'a@b.vn')).toEqual(['customerService.resend.error.phone']);
  });
});

describe('complaint workflow', () => {
  it('lets an open complaint start review, reject, resolve or be edited', () => {
    expect(complaintActions(Status.Open)).toEqual(['edit', 'startReview', 'reject', 'resolve']);
  });

  it('lets an in-review complaint only reject, resolve or be edited', () => {
    expect(complaintActions(Status.InReview)).toEqual(['edit', 'reject', 'resolve']);
  });

  it('offers nothing once closed', () => {
    expect(complaintActions(Status.Resolved)).toEqual([]);
    expect(complaintActions(Status.Rejected)).toEqual([]);
    expect(isComplaintClosed(Status.Resolved)).toBe(true);
    expect(isComplaintClosed(Status.Rejected)).toBe(true);
    expect(isComplaintClosed(Status.InReview)).toBe(false);
  });

  it('needs approval for every compensation but an apology', () => {
    expect(resolutionNeedsApproval(Resolution.Refund)).toBe(true);
    expect(resolutionNeedsApproval(Resolution.GiftCard)).toBe(true);
    expect(resolutionNeedsApproval(Resolution.Points)).toBe(true);
    expect(resolutionNeedsApproval(Resolution.Apology)).toBe(false);
    expect(resolutionNeedsApproval(Resolution.None)).toBe(false);
  });

  it('asks a non-approver for the PIN but not an approver', () => {
    expect(resolutionNeedsPin(Resolution.GiftCard, false)).toBe(true);
    expect(resolutionNeedsPin(Resolution.GiftCard, true)).toBe(false);
    expect(resolutionNeedsPin(Resolution.Apology, false)).toBe(false);
  });

  it('restricts resolutions to what the complaint is linked to', () => {
    expect(availableResolutions({ hasInvoice: false, hasCustomer: false })).toEqual([Resolution.Apology]);
    expect(availableResolutions({ hasInvoice: true, hasCustomer: false })).toEqual([Resolution.Refund, Resolution.Apology]);
    expect(availableResolutions({ hasInvoice: false, hasCustomer: true })).toEqual([Resolution.GiftCard, Resolution.Points, Resolution.Apology]);
  });
});

describe('resolveErrors', () => {
  it('accepts an apology with nothing else', () => {
    expect(resolveErrors(form())).toEqual([]);
  });

  it('requires a resolution that the links allow', () => {
    expect(resolveErrors(form({ resolution: null }))).toEqual(['customerService.resolve.error.resolution']);
    expect(resolveErrors(form({ resolution: Resolution.None }))).toEqual(['customerService.resolve.error.resolution']);
    expect(resolveErrors(form({ resolution: Resolution.Refund, hasInvoice: false }))).toEqual(['customerService.resolve.error.resolution']);
  });

  it('requires a positive amount for a gift card and whole points', () => {
    expect(resolveErrors(form({ resolution: Resolution.GiftCard, amount: 0 }))).toEqual(['customerService.resolve.error.amount']);
    expect(resolveErrors(form({ resolution: Resolution.GiftCard, amount: 50_000 }))).toEqual([]);
    expect(resolveErrors(form({ resolution: Resolution.Points, amount: 10.5 }))).toEqual(['customerService.resolve.error.pointsWhole']);
    expect(resolveErrors(form({ resolution: Resolution.Points, amount: 100 }))).toEqual([]);
  });

  it('requires a reference for a non-cash refund tender', () => {
    expect(resolveErrors(form({ resolution: Resolution.Refund, refundTender: Tender.Card }))).toEqual(['customerService.resolve.error.reference']);
    expect(resolveErrors(form({ resolution: Resolution.Refund, refundTender: Tender.Card, refundReference: 'R1' }))).toEqual([]);
    expect(resolveErrors(form({ resolution: Resolution.Refund, refundTender: Tender.Cash }))).toEqual([]);
  });
});

describe('complaint helpers', () => {
  it('checks the description length', () => {
    expect(isValidComplaintDescription('  ab ')).toBe(false);
    expect(isValidComplaintDescription('abc')).toBe(true);
  });

  it('builds filters without blanks and with the picked theater', () => {
    expect(complaintFilters({ status: '1', category: '' }, 'T1')).toEqual({ theaterId: 'T1', status: '1' });
    expect(complaintFilters({ status: '', category: null })).toEqual({});
  });
});

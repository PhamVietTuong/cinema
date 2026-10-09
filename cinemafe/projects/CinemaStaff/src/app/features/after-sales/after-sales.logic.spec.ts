import { CinemaServiceAgent, UserRoles } from 'CinemaLib';
import {
  canExchangeInvoice,
  canReprintInvoice,
  hasSearchCriteria,
  isOverrideRejection,
  isValidReprintReason,
  needsManagerOverride,
  refundFormErrors,
} from './after-sales.logic';

const Reason = CinemaServiceAgent.StaffReasonCode;
const Tender = CinemaServiceAgent.PaymentTender;

describe('after-sales logic', () => {
  describe('needsManagerOverride', () => {
    it('is required for sellers who are not approvers', () => {
      expect(needsManagerOverride(UserRoles.BoxOfficeStaff)).toBe(true);
      expect(needsManagerOverride(UserRoles.TheaterStaff)).toBe(true);
      expect(needsManagerOverride(null)).toBe(true);
    });

    it('is skipped for approver roles', () => {
      expect(needsManagerOverride(UserRoles.Admin)).toBe(false);
      expect(needsManagerOverride(UserRoles.TheaterManager)).toBe(false);
      expect(needsManagerOverride(UserRoles.RegionalManager)).toBe(false);
    });
  });

  describe('isOverrideRejection', () => {
    const forbidden = (message: string) => ({ status: 403, response: JSON.stringify({ error: message, statusCode: 403 }) });

    it('detects a 403 about manager approval', () => {
      expect(isOverrideRejection(forbidden('Manager approval is required for this action.'))).toBe(true);
      expect(isOverrideRejection(forbidden('Manager approval was refused.'))).toBe(true);
    });

    it('ignores other failures', () => {
      expect(isOverrideRejection(forbidden('You do not have access to this theater.'))).toBe(false);
      expect(isOverrideRejection({ status: 500, response: JSON.stringify({ error: 'Manager approval' }) })).toBe(false);
      expect(isOverrideRejection(null)).toBe(false);
    });
  });

  describe('refundFormErrors', () => {
    it('is valid for a cash refund with a reason', () => {
      expect(refundFormErrors({ reasonCode: Reason.CustomerRequest, note: '', tender: Tender.Cash, reference: '' })).toEqual([]);
    });

    it('requires a reason and a tender', () => {
      const errors = refundFormErrors({ reasonCode: null, note: '', tender: null, reference: '' });
      expect(errors).toContain('afterSales.refund.error.reasonRequired');
      expect(errors).toContain('afterSales.refund.error.tenderRequired');
    });

    it('requires a note for reason Other', () => {
      expect(refundFormErrors({ reasonCode: Reason.Other, note: '  ', tender: Tender.Cash, reference: '' }))
        .toEqual(['afterSales.refund.error.noteRequired']);
      expect(refundFormErrors({ reasonCode: Reason.Other, note: 'why', tender: Tender.Cash, reference: '' })).toEqual([]);
    });

    it('requires a reference for card and QR refunds only', () => {
      expect(refundFormErrors({ reasonCode: Reason.WrongShowtime, note: '', tender: Tender.Card, reference: '' }))
        .toEqual(['afterSales.refund.error.referenceRequired']);
      expect(refundFormErrors({ reasonCode: Reason.WrongShowtime, note: '', tender: Tender.QrWallet, reference: 'TX-1' })).toEqual([]);
    });
  });

  it('requires some search criterion', () => {
    expect(hasSearchCriteria('', '  ')).toBe(false);
    expect(hasSearchCriteria('INV-1', '')).toBe(true);
    expect(hasSearchCriteria(null, '0900000000')).toBe(true);
  });

  it('needs a reprint reason of at least 3 characters', () => {
    expect(isValidReprintReason('  ab ')).toBe(false);
    expect(isValidReprintReason('lost')).toBe(true);
  });

  describe('invoice action rules', () => {
    const invoice = (patch: Partial<CinemaServiceAgent.IAfterSalesInvoiceDTO>) => CinemaServiceAgent.AfterSalesInvoiceDTO.fromJS({
      status: CinemaServiceAgent.InvoiceStatus.Paid,
      channel: CinemaServiceAgent.SalesChannel.Counter,
      usedTicketCount: 0,
      canRefund: true,
      ...patch,
    });

    it('exchanges only paid, unused, refundable counter sales', () => {
      expect(canExchangeInvoice(invoice({}))).toBe(true);
      expect(canExchangeInvoice(invoice({ channel: CinemaServiceAgent.SalesChannel.Online }))).toBe(false);
      expect(canExchangeInvoice(invoice({ usedTicketCount: 1 }))).toBe(false);
      expect(canExchangeInvoice(invoice({ canRefund: false }))).toBe(false);
      expect(canExchangeInvoice(invoice({ status: CinemaServiceAgent.InvoiceStatus.Refunded }))).toBe(false);
    });

    it('reprints only paid invoices', () => {
      expect(canReprintInvoice(invoice({}))).toBe(true);
      expect(canReprintInvoice(invoice({ status: CinemaServiceAgent.InvoiceStatus.Pending }))).toBe(false);
    });
  });
});

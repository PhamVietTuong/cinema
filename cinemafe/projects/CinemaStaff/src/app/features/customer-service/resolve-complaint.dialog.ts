import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { ComplaintResolutionValues, RefundTenderValues, SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { availableResolutions, resolutionNeedsApproval, resolveErrors } from './customer-service.logic';

export interface ResolveComplaintDialogData {
  complaint: CinemaServiceAgent.ComplaintDTO;
  /** The signed-in user is an approver: no manager PIN will be asked. */
  isApprover: boolean;
}

/** What the dialog collects; the page sends it (with the manager override) through ResolveComplaint. */
export interface ResolveComplaintChoice {
  resolution: CinemaServiceAgent.ComplaintResolution;
  amount?: number;
  note?: string;
  refundTender: CinemaServiceAgent.PaymentTender;
  refundReference?: string;
}

/**
 * Compensation form of a complaint: resolution (a refund needs an invoice link, a gift card or points need the customer),
 * the amount for a gift card / points, the refund tender and reference for a refund, and a note.
 */
@Component({
  selector: 'staff-resolve-complaint-dialog',
  standalone: true,
  imports: [SharedModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './resolve-complaint.dialog.html',
  styleUrl: './resolve-complaint.dialog.scss',
})
export class ResolveComplaintDialogComponent {
  readonly data = inject<ResolveComplaintDialogData>(MAT_DIALOG_DATA);
  private readonly _ref = inject(MatDialogRef<ResolveComplaintDialogComponent, ResolveComplaintChoice>);

  readonly tenders = RefundTenderValues;
  readonly cashTender = CinemaServiceAgent.PaymentTender.Cash;
  private readonly _link = { hasInvoice: !!this.data.complaint.invoiceId, hasCustomer: !!this.data.complaint.customerUserId };
  readonly options = ComplaintResolutionValues.filter(v => availableResolutions(this._link).includes(v.value));

  readonly form = inject(FormBuilder).group({
    resolution: [this.options[0]?.value as CinemaServiceAgent.ComplaintResolution | null],
    amount: [null as number | null],
    refundTender: [CinemaServiceAgent.PaymentTender.Cash as CinemaServiceAgent.PaymentTender],
    refundReference: [''],
    note: [''],
  });
  private readonly _value = signal(this.form.getRawValue());

  readonly isRefund = computed(() => this._value().resolution === CinemaServiceAgent.ComplaintResolution.Refund);
  readonly isPoints = computed(() => this._value().resolution === CinemaServiceAgent.ComplaintResolution.Points);
  readonly isMoney = computed(() => this.isPoints() || this._value().resolution === CinemaServiceAgent.ComplaintResolution.GiftCard);
  readonly needsApproval = computed(() => resolutionNeedsApproval(this._value().resolution));
  readonly errors = computed(() => resolveErrors({ ...this._value(), ...this._link }));

  constructor() {
    this.form.valueChanges.subscribe(() => this._value.set(this.form.getRawValue()));
  }

  cancel(): void {
    this._ref.close();
  }

  confirm(): void {
    if (this.errors().length > 0) {
      return;
    }
    const v = this.form.getRawValue();
    const refund = v.resolution === CinemaServiceAgent.ComplaintResolution.Refund;
    this._ref.close({
      resolution: v.resolution!,
      amount: this.isMoney() ? (v.amount ?? undefined) : undefined,
      note: (v.note ?? '').trim() || undefined,
      refundTender: refund ? v.refundTender : CinemaServiceAgent.PaymentTender.Cash,
      refundReference: refund && v.refundTender !== this.cashTender ? (v.refundReference ?? '').trim() || undefined : undefined,
    });
  }
}

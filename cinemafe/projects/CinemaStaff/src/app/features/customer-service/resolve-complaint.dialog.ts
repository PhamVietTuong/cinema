import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { ComplaintResolutionValues, RefundTenderValues, SharedModule, StaffServiceAgent } from 'CinemaLib';
import { availableResolutions, resolutionNeedsApproval, resolveErrors } from './customer-service.logic';

export interface ResolveComplaintDialogData {
  complaint: StaffServiceAgent.ComplaintDTO;
  /** The signed-in user is an approver: no manager PIN will be asked. */
  isApprover: boolean;
}

/** What the dialog collects; the page sends it (with the manager override) through ResolveComplaint. */
export interface ResolveComplaintChoice {
  resolution: StaffServiceAgent.ComplaintResolution;
  amount?: number;
  note?: string;
  refundTender: StaffServiceAgent.PaymentTender;
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
  template: `
<div mat-dialog-title class="dialog-title">{{ 'customerService.resolve.title' | translate }}</div>
<mat-dialog-content>
  @if (needsApproval()) {
    <p class="notice"><mat-icon>admin_panel_settings</mat-icon>
      {{ (data.isApprover ? 'customerService.resolve.approverHint' : 'customerService.resolve.pinHint') | translate }}</p>
  }
  <form [formGroup]="form" class="fields">
    <mat-form-field appearance="outline" subscriptSizing="dynamic">
      <mat-label>{{ 'customerService.resolve.resolution' | translate }}</mat-label>
      <mat-select formControlName="resolution">
        @for (option of options; track option.value) {
          <mat-option [value]="option.value">{{ option.name | translate }}</mat-option>
        }
      </mat-select>
    </mat-form-field>

    @if (isMoney()) {
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>{{ (isPoints() ? 'customerService.resolve.points' : 'customerService.resolve.amount') | translate }}</mat-label>
        <input matInput type="number" min="0" [step]="isPoints() ? 1 : 1000" formControlName="amount">
      </mat-form-field>
    }

    @if (isRefund()) {
      <p class="hint">{{ 'customerService.resolve.refundHint' | translate: { code: data.complaint.invoiceCode } }}</p>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>{{ 'customerService.resolve.refundTender' | translate }}</mat-label>
        <mat-select formControlName="refundTender">
          @for (option of tenders; track option.value) {
            <mat-option [value]="option.value">{{ option.name | translate }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      @if (form.value.refundTender !== cashTender) {
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ 'customerService.resolve.refundReference' | translate }}</mat-label>
          <input matInput maxlength="100" formControlName="refundReference" autocomplete="off">
        </mat-form-field>
      }
    }

    <mat-form-field appearance="outline" subscriptSizing="dynamic">
      <mat-label>{{ 'customerService.resolve.note' | translate }}</mat-label>
      <textarea matInput rows="3" maxlength="500" formControlName="note"></textarea>
    </mat-form-field>
  </form>
  @if (errors().length) {
    <p class="error">{{ errors()[0] | translate }}</p>
  }
</mat-dialog-content>
<div mat-dialog-actions class="dialog-actions">
  <button mat-button type="button" (click)="cancel()">{{ 'common.cancel' | translate }}</button>
  <button mat-raised-button color="primary" type="button" [disabled]="errors().length > 0" (click)="confirm()">
    <mat-icon>task_alt</mat-icon> {{ 'customerService.resolve.confirm' | translate }}
  </button>
</div>
`,
  styles: [`
    .fields { display: flex; flex-direction: column; gap: 12px; min-width: 360px; }
    .notice { display: flex; align-items: center; gap: 8px; color: var(--ml-muted); margin: 0 0 12px; }
    .hint { color: var(--ml-muted); margin: 0; }
    .error { color: var(--ml-warn-ink); margin: 8px 0 0; }
  `],
})
export class ResolveComplaintDialogComponent {
  readonly data = inject<ResolveComplaintDialogData>(MAT_DIALOG_DATA);
  private readonly _ref = inject(MatDialogRef<ResolveComplaintDialogComponent, ResolveComplaintChoice>);

  readonly tenders = RefundTenderValues;
  readonly cashTender = StaffServiceAgent.PaymentTender.Cash;
  private readonly _link = { hasInvoice: !!this.data.complaint.invoiceId, hasCustomer: !!this.data.complaint.customerUserId };
  readonly options = ComplaintResolutionValues.filter(v => availableResolutions(this._link).includes(v.value));

  readonly form = inject(FormBuilder).group({
    resolution: [this.options[0]?.value as StaffServiceAgent.ComplaintResolution | null],
    amount: [null as number | null],
    refundTender: [StaffServiceAgent.PaymentTender.Cash as StaffServiceAgent.PaymentTender],
    refundReference: [''],
    note: [''],
  });
  private readonly _value = signal(this.form.getRawValue());

  readonly isRefund = computed(() => this._value().resolution === StaffServiceAgent.ComplaintResolution.Refund);
  readonly isPoints = computed(() => this._value().resolution === StaffServiceAgent.ComplaintResolution.Points);
  readonly isMoney = computed(() => this.isPoints() || this._value().resolution === StaffServiceAgent.ComplaintResolution.GiftCard);
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
    const refund = v.resolution === StaffServiceAgent.ComplaintResolution.Refund;
    this._ref.close({
      resolution: v.resolution!,
      amount: this.isMoney() ? (v.amount ?? undefined) : undefined,
      note: (v.note ?? '').trim() || undefined,
      refundTender: refund ? v.refundTender : StaffServiceAgent.PaymentTender.Cash,
      refundReference: refund && v.refundTender !== this.cashTender ? (v.refundReference ?? '').trim() || undefined : undefined,
    });
  }
}

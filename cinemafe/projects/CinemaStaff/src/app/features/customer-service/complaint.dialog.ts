import { ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TranslateService } from '@ngx-translate/core';
import { ComplaintCategoryValues, SharedModule, StaffServiceAgent, apiErrorMessage } from 'CinemaLib';
import { isValidComplaintDescription } from './customer-service.logic';

export interface ComplaintDialogData {
  /** Theater to file the complaint under (an Admin's picked theater); the API uses the caller's own otherwise. */
  theaterId?: string;
  /** Customer the complaint is about, from the lookup. */
  customer?: StaffServiceAgent.CustomerCardDTO | null;
  /** Invoice the complaint is about, from the lookup. */
  invoice?: { id: string; code: string } | null;
  /** Set to edit an open or in-review complaint instead of creating one. */
  complaint?: StaffServiceAgent.ComplaintDTO;
}

/** Creates a complaint (prefilled from the looked-up customer / invoice) or edits its category and description. Closes with the saved complaint. */
@Component({
  selector: 'staff-complaint-dialog',
  standalone: true,
  imports: [SharedModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
<div mat-dialog-title class="dialog-title">{{ (data.complaint ? 'customerService.complaint.editTitle' : 'customerService.complaint.createTitle') | translate }}</div>
<mat-dialog-content>
  @if (linkedCustomer || linkedInvoice) {
    <p class="links">
      @if (linkedCustomer) {
        <span class="ad-pill ad-pill--neutral">{{ 'customerService.complaint.customer' | translate }}: {{ linkedCustomer }}</span>
      }
      @if (linkedInvoice) {
        <span class="ad-pill ad-pill--neutral">{{ 'customerService.complaint.invoice' | translate }}: {{ linkedInvoice }}</span>
      }
    </p>
  }
  <form [formGroup]="form" class="fields">
    <mat-form-field appearance="outline" subscriptSizing="dynamic">
      <mat-label>{{ 'customerService.complaint.category' | translate }}</mat-label>
      <mat-select formControlName="category">
        @for (option of categories; track option.value) {
          <mat-option [value]="option.value">{{ option.name | translate }}</mat-option>
        }
      </mat-select>
    </mat-form-field>
    <mat-form-field appearance="outline" subscriptSizing="dynamic">
      <mat-label>{{ 'customerService.complaint.description' | translate }}</mat-label>
      <textarea matInput rows="5" maxlength="2000" formControlName="description"></textarea>
    </mat-form-field>
  </form>
  @if (serverError()) {
    <p class="error">{{ serverError() }}</p>
  }
</mat-dialog-content>
<div mat-dialog-actions class="dialog-actions">
  <button mat-button type="button" (click)="cancel()">{{ 'common.cancel' | translate }}</button>
  <button mat-raised-button color="primary" type="button" [disabled]="busy() || !valid()" (click)="save()">
    {{ 'common.save' | translate }}
  </button>
</div>
`,
  styles: [`
    .links { display: flex; gap: 8px; flex-wrap: wrap; margin: 0 0 12px; }
    .fields { display: flex; flex-direction: column; gap: 12px; min-width: 360px; }
    .error { color: var(--ml-warn-ink); margin: 8px 0 0; }
  `],
})
export class ComplaintDialogComponent {
  readonly data = inject<ComplaintDialogData>(MAT_DIALOG_DATA);
  private readonly _ref = inject(MatDialogRef<ComplaintDialogComponent, StaffServiceAgent.ComplaintDTO>);
  private readonly _api = inject(StaffServiceAgent.CustomerServiceHttpService);
  private readonly _translate = inject(TranslateService);
  private readonly _cdr = inject(ChangeDetectorRef);

  readonly categories = ComplaintCategoryValues;
  readonly form = inject(FormBuilder).group({
    category: [this.data.complaint?.category ?? StaffServiceAgent.ComplaintCategory.Other, Validators.required],
    description: [this.data.complaint?.description ?? '', Validators.required],
  });
  readonly busy = signal(false);
  readonly serverError = signal('');

  readonly linkedCustomer = this.data.complaint?.customerName ?? this.data.customer?.name ?? '';
  readonly linkedInvoice = this.data.complaint?.invoiceCode ?? this.data.invoice?.code ?? '';

  valid(): boolean {
    return isValidComplaintDescription(this.form.value.description);
  }

  cancel(): void {
    this._ref.close();
  }

  save(): void {
    if (!this.valid()) {
      return;
    }
    const { category, description } = this.form.getRawValue();
    this.busy.set(true);
    const call = this.data.complaint
      ? this._api.updateComplaint(StaffServiceAgent.UpdateComplaintRequest.fromJS({
        complaintId: this.data.complaint.id,
        category,
        description: (description ?? '').trim(),
      }))
      : this._api.createComplaint(StaffServiceAgent.CreateComplaintRequest.fromJS({
        theaterId: this.data.theaterId || undefined,
        category,
        description: (description ?? '').trim(),
        customerUserId: this.data.customer?.id || undefined,
        invoiceId: this.data.invoice?.id || undefined,
      }));
    call.subscribe({
      next: complaint => this._ref.close(complaint),
      error: error => {
        this.busy.set(false);
        this.serverError.set(apiErrorMessage(error, this._translate.instant('customerService.complaint.saveFailed')));
        this._cdr.markForCheck();
      },
    });
  }
}

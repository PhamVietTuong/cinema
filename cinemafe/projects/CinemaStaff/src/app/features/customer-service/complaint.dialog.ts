import { ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TranslateService } from '@ngx-translate/core';
import { ComplaintCategoryValues, SharedModule, CinemaServiceAgent, apiErrorMessage } from 'CinemaLib';
import { isValidComplaintDescription } from './customer-service.logic';

export interface ComplaintDialogData {
  /** Theater to file the complaint under (an Admin's picked theater); the API uses the caller's own otherwise. */
  theaterId?: string;
  /** Customer the complaint is about, from the lookup. */
  customer?: CinemaServiceAgent.CustomerCardDTO | null;
  /** Invoice the complaint is about, from the lookup. */
  invoice?: { id: string; code: string } | null;
  /** Set to edit an open or in-review complaint instead of creating one. */
  complaint?: CinemaServiceAgent.ComplaintDTO;
}

/** Creates a complaint (prefilled from the looked-up customer / invoice) or edits its category and description. Closes with the saved complaint. */
@Component({
  selector: 'staff-complaint-dialog',
  standalone: true,
  imports: [SharedModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './complaint.dialog.html',
  styleUrl: './complaint.dialog.scss',
})
export class ComplaintDialogComponent {
  readonly data = inject<ComplaintDialogData>(MAT_DIALOG_DATA);
  private readonly _ref = inject(MatDialogRef<ComplaintDialogComponent, CinemaServiceAgent.ComplaintDTO>);
  private readonly _api = inject(CinemaServiceAgent.HttpService);
  private readonly _translate = inject(TranslateService);
  private readonly _cdr = inject(ChangeDetectorRef);

  readonly categories = ComplaintCategoryValues;
  readonly form = inject(FormBuilder).group({
    category: [this.data.complaint?.category ?? CinemaServiceAgent.ComplaintCategory.Other, Validators.required],
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
      ? this._api.updateComplaint(CinemaServiceAgent.UpdateComplaintRequest.fromJS({
        complaintId: this.data.complaint.id,
        category,
        description: (description ?? '').trim(),
      }))
      : this._api.createComplaint(CinemaServiceAgent.CreateComplaintRequest.fromJS({
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

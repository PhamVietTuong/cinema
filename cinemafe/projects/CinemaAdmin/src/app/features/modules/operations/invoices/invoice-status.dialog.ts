import { Component, Inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import {
  CinemaServiceAgent,
  InvoiceStatusValues,
  showLoading, hideLoading, showSuccess, showException,
} from 'CinemaLib';

type Dto = CinemaServiceAgent.InvoiceAdminDTO;

export interface InvoiceStatusDialogData {
  invoice: Dto;
}

/** Status-change form for an invoice, opened via MatDialog. Resolves `true` on save, `false` on cancel. */
@Component({
  selector: 'app-invoice-status-dialog',
  standalone: false,
  templateUrl: './invoice-status.dialog.html',
})
export class InvoiceStatusDialog {
  readonly editingId: string;
  readonly statuses = InvoiceStatusValues;
  form: FormGroup;

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _store: Store<any>,
    private _dialogRef: MatDialogRef<InvoiceStatusDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) data: InvoiceStatusDialogData,
  ) {
    this.editingId = data.invoice.id ?? '';
    this.form = this._fb.group({
      status: [data.invoice.status, Validators.required],
    });
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    this._store.dispatch(showLoading());
    this._svc.updateInvoiceStatus(CinemaServiceAgent.UpdateInvoiceStatusRequest.fromJS({
      id: this.editingId, status: this.form.value.status,
    })).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({}));
        this._dialogRef.close(true);
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }

  cancel(): void {
    this._dialogRef.close(false);
  }
}

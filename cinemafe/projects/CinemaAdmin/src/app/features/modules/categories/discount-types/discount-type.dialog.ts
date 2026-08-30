import { Component, Inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CinemaServiceAgent, showLoading, hideLoading, showSuccess, showException } from 'CinemaLib';

type Dto = CinemaServiceAgent.DiscountTypeDTO;

export interface DiscountTypeDialogData {
  discountType: Dto | null;
}

/** Create/edit form for a discount type, opened via MatDialog. Resolves `true` on save, `false` on cancel. */
@Component({
  selector: 'app-discount-type-dialog',
  standalone: false,
  templateUrl: './discount-type.dialog.html',
})
export class DiscountTypeDialog {
  readonly editingId: string | null;
  form: FormGroup;

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _store: Store<any>,
    private _dialogRef: MatDialogRef<DiscountTypeDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) data: DiscountTypeDialogData,
  ) {
    this.editingId = data.discountType?.id ?? null;
    this.form = this._fb.group({
      name: [data.discountType?.name ?? '', Validators.required],
    });
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    const obs = this.editingId
      ? this._svc.updateDiscountType(CinemaServiceAgent.UpdateDiscountTypeRequest.fromJS({ ...v, id: this.editingId }))
      : this._svc.createDiscountType(CinemaServiceAgent.CreateDiscountTypeRequest.fromJS(v));

    this._store.dispatch(showLoading());
    obs.subscribe({
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

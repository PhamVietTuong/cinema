import { Component, Inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CinemaServiceAgent, showLoading, hideLoading, showSuccess, showException } from 'CinemaLib';

type Dto = CinemaServiceAgent.HolidayDTO;

export interface HolidayDialogData {
  holiday: Dto | null;
}

/** Create/edit form for a holiday, opened via MatDialog. Resolves `true` on save, `false` on cancel. */
@Component({
  selector: 'app-holiday-dialog',
  standalone: false,
  templateUrl: './holiday.dialog.html',
})
export class HolidayDialog {
  readonly editingId: string | null;
  form: FormGroup;

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _store: Store<any>,
    private _dialogRef: MatDialogRef<HolidayDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) data: HolidayDialogData,
  ) {
    this.editingId = data.holiday?.id ?? null;
    this.form = this._fb.group({
      name: [data.holiday?.name ?? '', Validators.required],
      date: [data.holiday?.date ? new Date(data.holiday.date).toISOString().split('T')[0] : '', Validators.required],
      priceMultiplier: [data.holiday?.priceMultiplier ?? 1.5, [Validators.required, Validators.min(0)]],
    });
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    const obs = this.editingId
      ? this._svc.updateHoliday(CinemaServiceAgent.UpdateHolidayRequest.fromJS({ ...v, id: this.editingId }))
      : this._svc.createHoliday(CinemaServiceAgent.CreateHolidayRequest.fromJS(v));

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

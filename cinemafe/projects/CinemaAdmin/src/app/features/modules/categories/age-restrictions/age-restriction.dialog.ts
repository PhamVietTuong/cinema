import { Component, Inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CinemaServiceAgent, showLoading, hideLoading, showSuccess, showException } from 'CinemaLib';

type Dto = CinemaServiceAgent.AgeRestrictionDTO;

export interface AgeRestrictionDialogData {
  ageRestriction: Dto | null;
}

/** Create/edit form for an age restriction, opened via MatDialog. Resolves `true` on save, `false` on cancel. */
@Component({
  selector: 'app-age-restriction-dialog',
  standalone: false,
  templateUrl: './age-restriction.dialog.html',
})
export class AgeRestrictionDialog {
  readonly editingId: string | null;
  form: FormGroup;

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _store: Store<any>,
    private _dialogRef: MatDialogRef<AgeRestrictionDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) data: AgeRestrictionDialogData,
  ) {
    this.editingId = data.ageRestriction?.id ?? null;
    this.form = this._fb.group({
      code: [data.ageRestriction?.code ?? '', Validators.required],
      description: [data.ageRestriction?.description ?? '', Validators.required],
      minAge: [data.ageRestriction?.minAge ?? 0, [Validators.required, Validators.min(0)]],
    });
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    const obs = this.editingId
      ? this._svc.updateAgeRestriction(CinemaServiceAgent.UpdateAgeRestrictionRequest.fromJS({ ...v, id: this.editingId }))
      : this._svc.createAgeRestriction(CinemaServiceAgent.CreateAgeRestrictionRequest.fromJS(v));

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

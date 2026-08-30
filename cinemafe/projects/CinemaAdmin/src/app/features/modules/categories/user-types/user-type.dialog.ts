import { Component, Inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CinemaServiceAgent, showLoading, hideLoading, showSuccess, showException } from 'CinemaLib';

type Dto = CinemaServiceAgent.UserTypeDTO;

export interface UserTypeDialogData {
  userType: Dto | null;
}

/** Create/edit form for a user type, opened via MatDialog. Resolves `true` on save, `false` on cancel. */
@Component({
  selector: 'app-user-type-dialog',
  standalone: false,
  templateUrl: './user-type.dialog.html',
})
export class UserTypeDialog {
  readonly editingId: string | null;
  form: FormGroup;

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _store: Store<any>,
    private _dialogRef: MatDialogRef<UserTypeDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) data: UserTypeDialogData,
  ) {
    this.editingId = data.userType?.id ?? null;
    this.form = this._fb.group({
      name: [data.userType?.name ?? '', Validators.required],
    });
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    const obs = this.editingId
      ? this._svc.updateUserType(CinemaServiceAgent.UpdateUserTypeRequest.fromJS({ ...v, id: this.editingId }))
      : this._svc.createUserType(CinemaServiceAgent.CreateUserTypeRequest.fromJS(v));

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

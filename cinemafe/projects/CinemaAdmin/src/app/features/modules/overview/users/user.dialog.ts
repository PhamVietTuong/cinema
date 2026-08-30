import { Component, Inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { TranslateService } from '@ngx-translate/core';
import { IdentityServiceAgent, CinemaServiceAgent, showLoading, hideLoading, showSuccess, showException } from 'CinemaLib';

const PHONE_PATTERN = /^(?:\+84|0)\d{9,10}$/;

type Dto = IdentityServiceAgent.UserDTO;

export interface UserDialogData {
  user: Dto | null;
}

/** Create/edit form for a user account, opened via MatDialog. Resolves `true` on save, `false` on cancel. */
@Component({
  selector: 'app-user-dialog',
  standalone: false,
  templateUrl: './user.dialog.html',
})
export class UserDialog {
  readonly editingId: string | null;
  readonly statuses: { v: IdentityServiceAgent.UserStatus; label: string }[];
  userTypes: CinemaServiceAgent.UserTypeDTO[] = [];
  form: FormGroup;

  constructor(
    private _identity: IdentityServiceAgent.HttpService,
    private _cinema: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _store: Store<any>,
    private _dialogRef: MatDialogRef<UserDialog, boolean>,
    private _translate: TranslateService,
    @Inject(MAT_DIALOG_DATA) data: UserDialogData,
  ) {
    this.editingId = data.user?.id ?? null;
    this.statuses = [
      { v: IdentityServiceAgent.UserStatus.Active, label: this._translate.instant('users.status.active') },
      { v: IdentityServiceAgent.UserStatus.Inactive, label: this._translate.instant('users.status.locked') },
      { v: IdentityServiceAgent.UserStatus.Banned, label: this._translate.instant('users.status.banned') },
    ];
    this.form = this._fb.group({
      name: [data.user?.name ?? '', Validators.required],
      email: [{ value: data.user?.email ?? '', disabled: !!data.user }, [Validators.required, Validators.email]],
      phone: [data.user?.phone ?? '', [Validators.required, Validators.pattern(PHONE_PATTERN)]],
      password: [{ value: '', disabled: !!data.user }, data.user ? [] : [Validators.required, Validators.minLength(6)]],
      userTypeId: [data.user?.userTypeId ?? '', Validators.required],
      status: [data.user?.status ?? IdentityServiceAgent.UserStatus.Active, Validators.required],
    });

    this._cinema.getUserTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200 }))
      .subscribe(r => { this.userTypes = r.results ?? []; });
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const obs = this.editingId
      ? this._identity.updateUser(IdentityServiceAgent.UpdateUserRequest.fromJS({
          id: this.editingId, name: v.name, phone: v.phone, userTypeId: v.userTypeId, status: v.status,
        }))
      : this._identity.createUser(IdentityServiceAgent.CreateUserRequest.fromJS({
          name: v.name, email: v.email, phone: v.phone, password: v.password, userTypeId: v.userTypeId, status: v.status,
        }));

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

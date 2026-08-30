import { Component, Inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CinemaServiceAgent, showLoading, hideLoading, showSuccess, showException } from 'CinemaLib';

type Dto = CinemaServiceAgent.MemberShipDTO;

export interface MembershipDialogData {
  membership: Dto | null;
}

/** Create/edit form for a membership tier, opened via MatDialog. Resolves `true` on save, `false` on cancel. */
@Component({
  selector: 'app-membership-dialog',
  standalone: false,
  templateUrl: './membership.dialog.html',
})
export class MembershipDialog {
  readonly editingId: string | null;
  form: FormGroup;

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _store: Store<any>,
    private _dialogRef: MatDialogRef<MembershipDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) data: MembershipDialogData,
  ) {
    this.editingId = data.membership?.id ?? null;
    this.form = this._fb.group({
      name: [data.membership?.name ?? '', Validators.required],
      discountPercent: [data.membership?.discountPercent ?? 0, [Validators.required, Validators.min(0)]],
      minPoints: [data.membership?.minPoints ?? 0, [Validators.required, Validators.min(0)]],
      maxPoints: [data.membership?.maxPoints ?? 0, [Validators.required, Validators.min(0)]],
    });
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    const obs = this.editingId
      ? this._svc.updateMemberShip(CinemaServiceAgent.UpdateMemberShipRequest.fromJS({ ...v, id: this.editingId }))
      : this._svc.createMemberShip(CinemaServiceAgent.CreateMemberShipRequest.fromJS(v));

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

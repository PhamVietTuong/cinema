import { Component, Inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CinemaServiceAgent, showLoading, hideLoading, showSuccess, showException } from 'CinemaLib';

type Dto = CinemaServiceAgent.RoomTypeDTO;

export interface RoomTypeDialogData {
  theaterId: string;
  roomType: Dto | null;
}

/** Create/edit form for a theater's room type, opened via MatDialog. Resolves `true` on save, `false` on cancel. */
@Component({
  selector: 'app-room-type-dialog',
  standalone: false,
  templateUrl: './room-type.dialog.html',
})
export class RoomTypeDialog {
  readonly editingId: string | null;
  form: FormGroup;

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _store: Store<any>,
    private _dialogRef: MatDialogRef<RoomTypeDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) private _data: RoomTypeDialogData,
  ) {
    this.editingId = _data.roomType?.id ?? null;
    this.form = this._fb.group({
      name: [_data.roomType?.name ?? '', Validators.required],
      description: [_data.roomType?.description ?? ''],
      supportsThreeD: [_data.roomType?.supportsThreeD ?? false],
      threeDSurcharge: [_data.roomType?.threeDSurcharge ?? 0, [Validators.min(0)]],
      turnoverBufferMinutes: [_data.roomType?.turnoverBufferMinutes ?? 0, [Validators.min(0), Validators.max(240)]],
    });
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    const obs = this.editingId
      ? this._svc.updateRoomType(CinemaServiceAgent.UpdateRoomTypeRequest.fromJS({ ...v, id: this.editingId, theaterId: this._data.theaterId }))
      : this._svc.createRoomType(CinemaServiceAgent.CreateRoomTypeRequest.fromJS({ ...v, theaterId: this._data.theaterId }));

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

import { Component, Inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { SharedModule, CinemaServiceAgent, hideLoading, showError, showException, showLoading, showSuccess, toDateKey, toTimeKey, toWallClockUtc } from 'CinemaLib';
import { buildShiftSlot } from './shift-times';

export interface ShiftDialogData {
  theaterId: string;
  userId: string;
  userName: string;
  /** Day the shift begins (new shift) . */
  day: Date;
  /** Existing shift when editing. */
  shift?: CinemaServiceAgent.StaffShiftDTO;
}

/** Create, edit or delete one shift. Resolves true when the roster changed. */
@Component({
  selector: 'staff-shift-dialog',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './shift.dialog.html',
  styleUrl: './shift.dialog.scss',
})
export class ShiftDialog {
  form: FormGroup;

  constructor(
    fb: FormBuilder,
    private _workforce: CinemaServiceAgent.HttpService,
    private _store: Store<any>,
    private _translate: TranslateService,
    private _dialogRef: MatDialogRef<ShiftDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) public data: ShiftDialogData,
  ) {
    const shift = data.shift;
    this.form = fb.group({
      date: [toDateKey(shift?.startTime ?? data.day), Validators.required],
      start: [shift?.startTime ? toTimeKey(shift.startTime) : '09:00', Validators.required],
      end: [shift?.endTime ? toTimeKey(shift.endTime) : '17:00', Validators.required],
      note: [shift?.note ?? ''],
    });
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    const slot = buildShiftSlot(v.date, v.start, v.end);
    if (!slot) {
      this._store.dispatch(showError({ message: this._translate.instant('roster.errors.invalidTime') }));
      return;
    }
    this._store.dispatch(showLoading());
    this._workforce.saveShift(CinemaServiceAgent.SaveStaffShiftRequest.fromJS({
      id: this.data.shift?.id,
      theaterId: this.data.theaterId,
      userId: this.data.userId,
      startTime: toWallClockUtc(slot.start),
      endTime: toWallClockUtc(slot.end),
      note: (v.note as string).trim() || undefined,
    })).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({ message: this._translate.instant('roster.toast.saved') }));
        this._dialogRef.close(true);
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }

  remove(): void {
    if (!this.data.shift?.id) {
      return;
    }
    this._store.dispatch(showLoading());
    this._workforce.deleteShift(CinemaServiceAgent.DeleteStaffShiftRequest.fromJS({ shiftId: this.data.shift.id })).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({ message: this._translate.instant('roster.toast.deleted') }));
        this._dialogRef.close(true);
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }

  cancel(): void {
    this._dialogRef.close(false);
  }
}

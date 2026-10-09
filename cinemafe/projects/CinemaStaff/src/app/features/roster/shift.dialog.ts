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
  template: `
    <div mat-dialog-title class="dialog-title">{{ (data.shift ? 'roster.dialog.edit' : 'roster.dialog.new') | translate }}: {{ data.userName }}</div>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <div class="fields">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ 'opsCommon.date' | translate }}</mat-label>
            <input matInput type="date" formControlName="date">
            <mat-error>{{ 'common.required' | translate }}</mat-error>
          </mat-form-field>
          <div class="row">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ 'roster.dialog.start' | translate }}</mat-label>
              <input matInput type="time" formControlName="start">
              <mat-error>{{ 'common.required' | translate }}</mat-error>
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ 'roster.dialog.end' | translate }}</mat-label>
              <input matInput type="time" formControlName="end">
              <mat-error>{{ 'common.required' | translate }}</mat-error>
            </mat-form-field>
          </div>
          <p class="hint">{{ 'roster.dialog.overnightHint' | translate }}</p>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ 'opsCommon.note' | translate }}</mat-label>
            <input matInput maxlength="300" formControlName="note">
          </mat-form-field>
        </div>
      </mat-dialog-content>
      <div mat-dialog-actions class="dialog-actions">
        @if (data.shift) {
          <button mat-raised-button color="warn" type="button" (click)="remove()">{{ 'common.remove' | translate }}</button>
        }
        <span class="spacer"></span>
        <button mat-raised-button type="button" (click)="cancel()">{{ 'common.cancel' | translate }}</button>
        <button mat-raised-button color="primary" type="submit">{{ 'common.save' | translate }}</button>
      </div>
    </form>
    <button mat-icon-button type="button" class="dialog-close-btn" (click)="cancel()"><mat-icon>close</mat-icon></button>
  `,
  styles: [`
    .fields { display: flex; flex-direction: column; gap: 12px; margin-top: 8px; }
    .row { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    .hint { color: var(--ml-muted); font-size: 12px; margin: 0; }
    .spacer { flex: 1; }
  `],
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

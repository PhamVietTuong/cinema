import { Component, Inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import {
  IncidentCategoryValues, IncidentSeverityValues, SharedModule, CinemaServiceAgent,
  hideLoading, showException, showLoading, showSuccess,
} from 'CinemaLib';
import { RoomSeatPickerComponent } from '../../core/room-seat-picker.component';

export interface ReportIncidentDialogData {
  theaterId: string;
}

/** Report an incident (category, severity, title, optional room/seat). Resolves the created incident, or undefined on cancel. */
@Component({
  selector: 'staff-report-incident-dialog',
  standalone: true,
  imports: [SharedModule, RoomSeatPickerComponent],
  template: `
    <div mat-dialog-title class="dialog-title">{{ 'incidents.report.title' | translate }}</div>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <div class="fields">
          <div class="row">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ 'incidents.fields.category' | translate }}</mat-label>
              <mat-select formControlName="category">
                @for (c of categories; track c.value) {
                  <mat-option [value]="c.value">{{ c.name | translate }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ 'incidents.fields.severity' | translate }}</mat-label>
              <mat-select formControlName="severity">
                @for (s of severities; track s.value) {
                  <mat-option [value]="s.value">{{ s.name | translate }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          </div>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ 'incidents.fields.title' | translate }}</mat-label>
            <input matInput maxlength="200" formControlName="title">
            <mat-error>{{ 'common.required' | translate }}</mat-error>
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ 'incidents.fields.description' | translate }}</mat-label>
            <textarea matInput rows="4" maxlength="1000" formControlName="description"></textarea>
          </mat-form-field>
          <staff-room-seat-picker [group]="form" [theaterId]="data.theaterId" />
        </div>
      </mat-dialog-content>
      <div mat-dialog-actions class="dialog-actions">
        <button mat-raised-button type="button" (click)="cancel()">{{ 'common.cancel' | translate }}</button>
        <button mat-raised-button color="primary" type="submit">{{ 'incidents.report.submit' | translate }}</button>
      </div>
    </form>
    <button mat-icon-button type="button" class="dialog-close-btn" (click)="cancel()"><mat-icon>close</mat-icon></button>
  `,
  styles: [`
    .fields { display: flex; flex-direction: column; gap: 16px; margin-top: 8px; }
    .row { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
  `],
})
export class ReportIncidentDialog {
  readonly categories = IncidentCategoryValues;
  readonly severities = IncidentSeverityValues;
  form: FormGroup;

  constructor(
    fb: FormBuilder,
    private _ops: CinemaServiceAgent.HttpService,
    private _store: Store<any>,
    private _translate: TranslateService,
    private _dialogRef: MatDialogRef<ReportIncidentDialog, CinemaServiceAgent.IncidentDTO | undefined>,
    @Inject(MAT_DIALOG_DATA) public data: ReportIncidentDialogData,
  ) {
    this.form = fb.group({
      category: [CinemaServiceAgent.IncidentCategory.Other],
      severity: [CinemaServiceAgent.IncidentSeverity.Low],
      title: ['', [Validators.required, Validators.pattern(/\S/)]],
      description: [''],
      roomId: [''],
      seatId: [''],
    });
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    this._store.dispatch(showLoading());
    this._ops.reportIncident(CinemaServiceAgent.ReportIncidentRequest.fromJS({
      theaterId: this.data.theaterId,
      category: v.category,
      severity: v.severity,
      title: (v.title as string).trim(),
      description: ((v.description as string) ?? '').trim() || undefined,
      roomId: v.roomId || undefined,
      seatId: v.seatId || undefined,
    })).subscribe({
      next: incident => {
        this._store.dispatch(showSuccess({ message: this._translate.instant('incidents.toast.reported') }));
        this._dialogRef.close(incident);
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }

  cancel(): void {
    this._dialogRef.close(undefined);
  }
}

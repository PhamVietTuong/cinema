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
  templateUrl: './report-incident.dialog.html',
  styleUrl: './report-incident.dialog.scss',
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

import { Component, Inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { SharedModule, CinemaServiceAgent, StaffTaskStatusValues, hideLoading, showException, showLoading, showSuccess, toWallClockUtc } from 'CinemaLib';

export interface TaskDialogData {
  theaterId: string;
  staff: CinemaServiceAgent.TheaterStaffDTO[];
  task?: CinemaServiceAgent.StaffTaskDTO;
}

/** Local `yyyy-MM-ddTHH:mm` for a datetime-local input. */
function toLocalInput(date: Date | undefined): string {
  if (!date) {
    return '';
  }
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/** Create or edit a task and assign it to a staff member. Resolves true when saved. */
@Component({
  selector: 'staff-task-dialog',
  standalone: true,
  imports: [SharedModule],
  template: `
    <div mat-dialog-title class="dialog-title">{{ (data.task ? 'tasks.dialog.edit' : 'tasks.dialog.new') | translate }}</div>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <div class="fields">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ 'tasks.fields.title' | translate }}</mat-label>
            <input matInput maxlength="200" formControlName="title">
            <mat-error>{{ 'common.required' | translate }}</mat-error>
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ 'tasks.fields.description' | translate }}</mat-label>
            <textarea matInput rows="3" maxlength="1000" formControlName="description"></textarea>
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ 'tasks.fields.assignee' | translate }}</mat-label>
            <mat-select formControlName="assignedToUserId">
              @for (member of data.staff; track member.id) {
                <mat-option [value]="member.id">{{ member.name }}</mat-option>
              }
            </mat-select>
            <mat-error>{{ 'common.required' | translate }}</mat-error>
          </mat-form-field>
          <div class="row">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ 'tasks.fields.dueAt' | translate }}</mat-label>
              <input matInput type="datetime-local" formControlName="dueAt">
            </mat-form-field>
            @if (data.task) {
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-label>{{ 'common.status' | translate }}</mat-label>
                <mat-select formControlName="status">
                  @for (s of statuses; track s.value) {
                    <mat-option [value]="s.value">{{ s.name | translate }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>
            }
          </div>
        </div>
      </mat-dialog-content>
      <div mat-dialog-actions class="dialog-actions">
        <button mat-raised-button type="button" (click)="cancel()">{{ 'common.cancel' | translate }}</button>
        <button mat-raised-button color="primary" type="submit">{{ 'common.save' | translate }}</button>
      </div>
    </form>
    <button mat-icon-button type="button" class="dialog-close-btn" (click)="cancel()"><mat-icon>close</mat-icon></button>
  `,
  styles: [`
    .fields { display: flex; flex-direction: column; gap: 16px; margin-top: 8px; }
    .row { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
  `],
})
export class TaskDialog {
  readonly statuses = StaffTaskStatusValues;
  form: FormGroup;

  constructor(
    fb: FormBuilder,
    private _workforce: CinemaServiceAgent.HttpService,
    private _store: Store<any>,
    private _translate: TranslateService,
    private _dialogRef: MatDialogRef<TaskDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) public data: TaskDialogData,
  ) {
    const task = data.task;
    this.form = fb.group({
      title: [task?.title ?? '', [Validators.required, Validators.pattern(/\S/)]],
      description: [task?.description ?? ''],
      assignedToUserId: [task?.assignedToUserId ?? '', Validators.required],
      dueAt: [toLocalInput(task?.dueAt)],
      status: [task?.status ?? CinemaServiceAgent.StaffTaskStatus.Open],
    });
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    this._store.dispatch(showLoading());
    this._workforce.saveTask(CinemaServiceAgent.SaveStaffTaskRequest.fromJS({
      id: this.data.task?.id,
      theaterId: this.data.theaterId,
      assignedToUserId: v.assignedToUserId,
      title: (v.title as string).trim(),
      description: ((v.description as string) ?? '').trim() || undefined,
      dueAt: v.dueAt ? toWallClockUtc(new Date(v.dueAt)) : undefined,
      incidentId: this.data.task?.incidentId,
      checklistRunId: this.data.task?.checklistRunId,
      status: this.data.task ? v.status : undefined,
    })).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({ message: this._translate.instant('tasks.toast.saved') }));
        this._dialogRef.close(true);
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }

  cancel(): void {
    this._dialogRef.close(false);
  }
}

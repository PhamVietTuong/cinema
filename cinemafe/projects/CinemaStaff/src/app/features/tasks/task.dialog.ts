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
  templateUrl: './task.dialog.html',
  styleUrl: './task.dialog.scss',
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

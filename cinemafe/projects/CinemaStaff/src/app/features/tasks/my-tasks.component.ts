import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import {
  EmptyStateComponent, SharedModule, CinemaServiceAgent, StatusPillComponent, hideLoading, showException, showLoading,
} from 'CinemaLib';

/** The signed-in staff member's own tasks, with quick status changes. */
@Component({
  selector: 'staff-my-tasks',
  standalone: true,
  imports: [SharedModule, StatusPillComponent, EmptyStateComponent],
  template: `
<div class="ad-page">
  <div class="ad-page-header">
    <div>
      <h1 class="ad-h1">{{ 'tasks.my.title' | translate }}</h1>
      <p class="ad-sub">{{ 'tasks.my.subtitle' | translate }}</p>
    </div>
    <div class="ad-toolbar">
      <mat-slide-toggle [checked]="includeClosed" (change)="toggleClosed($event.checked)">{{ 'tasks.my.showClosed' | translate }}</mat-slide-toggle>
    </div>
  </div>

  @if (!tasks.length && !loading) {
    <mat-card class="ad-card--pad-0"><cl-empty-state icon="task_alt" messageKey="tasks.my.empty" /></mat-card>
  }
  @for (task of tasks; track task.id) {
    <mat-card class="task">
      <div class="main">
        <div class="title">
          <strong>{{ task.title }}</strong>
          <cl-status-pill kind="staffTask" [value]="task.status" />
        </div>
        @if (task.description) {
          <p class="desc">{{ task.description }}</p>
        }
        <span class="muted">
          @if (task.dueAt) { {{ 'tasks.fields.dueAt' | translate }}: {{ task.dueAt | date: 'dd/MM HH:mm' }} · }
          {{ 'tasks.fields.createdBy' | translate }}: {{ task.createdByName }}
        </span>
      </div>
      <div class="actions">
        @if (task.status === Status.Open) {
          <button mat-stroked-button type="button" [disabled]="busy" (click)="setStatus(task, Status.InProgress)">{{ 'tasks.my.start' | translate }}</button>
        }
        @if (task.status === Status.Open || task.status === Status.InProgress) {
          <button mat-raised-button color="primary" type="button" [disabled]="busy" (click)="setStatus(task, Status.Done)"><mat-icon>check</mat-icon> {{ 'tasks.my.done' | translate }}</button>
        }
        @if (task.status === Status.Done || task.status === Status.Cancelled) {
          <button mat-stroked-button type="button" [disabled]="busy" (click)="setStatus(task, Status.Open)">{{ 'tasks.my.reopen' | translate }}</button>
        }
      </div>
    </mat-card>
  }
</div>
`,
  styles: [`
    .task { display: flex; justify-content: space-between; gap: 16px; padding: 16px; margin-bottom: 12px; flex-wrap: wrap; }
    .main { flex: 1 1 300px; }
    .title { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
    .desc { margin: 6px 0; }
    .muted { color: var(--ml-muted); font-size: 12px; }
    .actions { display: flex; gap: 8px; align-items: center; }
  `],
})
export class MyTasksComponent implements OnInit {
  readonly Status = CinemaServiceAgent.StaffTaskStatus;

  tasks: CinemaServiceAgent.StaffTaskDTO[] = [];
  includeClosed = false;
  loading = false;
  busy = false;

  private readonly _workforce = inject(CinemaServiceAgent.HttpService);
  private readonly _store = inject(Store);
  private readonly _cd = inject(ChangeDetectorRef);

  ngOnInit(): void {
    this.load();
  }

  toggleClosed(value: boolean): void {
    this.includeClosed = value;
    this.load();
  }

  load(): void {
    this.loading = true;
    this._store.dispatch(showLoading());
    this._workforce.getMyTasks(CinemaServiceAgent.MyTasksRequest.fromJS({ includeClosed: this.includeClosed })).subscribe({
      next: tasks => {
        this.tasks = tasks ?? [];
        this._cd.markForCheck();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this.loading = false;
      this._store.dispatch(hideLoading());
      this._cd.markForCheck();
    });
  }

  setStatus(task: CinemaServiceAgent.StaffTaskDTO, status: CinemaServiceAgent.StaffTaskStatus): void {
    this.busy = true;
    this._store.dispatch(showLoading());
    this._workforce.setMyTaskStatus(CinemaServiceAgent.SetMyTaskStatusRequest.fromJS({ taskId: task.id, status })).subscribe({
      next: () => this.load(),
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this.busy = false;
      this._store.dispatch(hideLoading());
      this._cd.markForCheck();
    });
  }
}

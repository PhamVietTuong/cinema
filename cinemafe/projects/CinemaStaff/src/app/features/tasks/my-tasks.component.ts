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
  templateUrl: './my-tasks.component.html',
  styleUrl: './my-tasks.component.scss',
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

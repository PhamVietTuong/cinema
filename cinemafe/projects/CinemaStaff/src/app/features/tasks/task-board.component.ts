import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { Store } from '@ngrx/store';
import { Subject } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import {
  EmptyStateComponent, FilterBarComponent, FilterBarField, SharedModule, CinemaServiceAgent, StaffTaskStatusValues,
  StatusPillComponent, hideLoading, showException, showLoading,
} from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';
import { TaskDialog } from './task.dialog';

/** Manager task board: every task of the theater with status / assignee filters, and create / edit with assignment. */
@Component({
  selector: 'staff-task-board',
  standalone: true,
  imports: [SharedModule, StatusPillComponent, EmptyStateComponent, FilterBarComponent],
  templateUrl: './task-board.component.html',
  styleUrl: './task-board.component.scss',
})
export class TaskBoardComponent implements OnInit {
  readonly pageSize = 10;

  filterFields: FilterBarField[] = [
    { key: 'status', type: 'select', labelKey: 'common.status', options: StaffTaskStatusValues.map(s => ({ value: String(s.value), labelKey: s.name })) },
    { key: 'assignedTo', type: 'select', labelKey: 'tasks.fields.assignee', options: [] },
  ];

  searchForm: FormGroup;
  rows: CinemaServiceAgent.StaffTaskDTO[] = [];
  staff: CinemaServiceAgent.TheaterStaffDTO[] = [];
  total = 0;
  pageIndex = 0;
  loading = false;

  private readonly _workforce = inject(CinemaServiceAgent.HttpService);
  private readonly _theaterContext = inject(TheaterContextService);
  private readonly _store = inject(Store);
  private readonly _dialog = inject(MatDialog);
  private readonly _cd = inject(ChangeDetectorRef);
  private readonly _filterChange$ = new Subject<void>();

  constructor(fb: FormBuilder) {
    this.searchForm = fb.group({ status: [''], assignedTo: [''] });
    effect(() => {
      const theaterId = this._theaterContext.currentTheaterId();
      this.pageIndex = 0;
      if (theaterId) {
        this._loadStaff(theaterId);
      }
      this.load();
    });
  }

  get theaterId(): string {
    return this._theaterContext.currentTheaterId() ?? '';
  }

  ngOnInit(): void {
    this._filterChange$.pipe(debounceTime(300)).subscribe(() => {
      this.pageIndex = 0;
      this.load();
    });
  }

  onFilterChange(): void {
    this._filterChange$.next();
  }

  onPage(pageIndex: number): void {
    this.pageIndex = pageIndex;
    this.load();
  }

  load(): void {
    const theaterId = this.theaterId;
    if (!theaterId) {
      this.rows = [];
      this.total = 0;
      this._cd.markForCheck();
      return;
    }
    const filters: { [key: string]: string } = { theaterId };
    const raw = this.searchForm.value as Record<string, string>;
    for (const key of Object.keys(raw)) {
      const value = (raw[key] ?? '').toString().trim();
      if (value) {
        filters[key] = value;
      }
    }
    this.loading = true;
    this._store.dispatch(showLoading());
    this._workforce.getTasks(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: this.pageIndex + 1, pageSize: this.pageSize, filters,
    })).subscribe({
      next: result => {
        this.rows = result.results ?? [];
        this.total = result.totalCount ?? 0;
        this._cd.markForCheck();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this.loading = false;
      this._store.dispatch(hideLoading());
      this._cd.markForCheck();
    });
  }

  openDialog(task?: CinemaServiceAgent.StaffTaskDTO): void {
    this._dialog.open(TaskDialog, { width: '520px', maxWidth: '95vw', data: { theaterId: this.theaterId, staff: this.staff, task } })
      .afterClosed().subscribe(saved => {
        if (saved) {
          this.load();
        }
      });
  }

  private _loadStaff(theaterId: string): void {
    this._workforce.getTheaterStaff(CinemaServiceAgent.GetTheaterStaffRequest.fromJS({ theaterId })).subscribe({
      next: staff => {
        this.staff = staff ?? [];
        // New array so the filter bar re-renders with the assignee options.
        this.filterFields = this.filterFields.map(field => field.key === 'assignedTo'
          ? { ...field, options: this.staff.map(s => ({ value: s.id!, label: s.name })) }
          : field);
        this._cd.markForCheck();
      },
      error: error => this._store.dispatch(showException({ error })),
    });
  }
}

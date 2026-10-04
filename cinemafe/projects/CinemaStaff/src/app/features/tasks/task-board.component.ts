import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { Store } from '@ngrx/store';
import { Subject } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import {
  EmptyStateComponent, FilterBarComponent, FilterBarField, SharedModule, StaffServiceAgent, StaffTaskStatusValues,
  StatusPillComponent, hideLoading, showException, showLoading,
} from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';
import { TaskDialog } from './task.dialog';

/** Manager task board: every task of the theater with status / assignee filters, and create / edit with assignment. */
@Component({
  selector: 'staff-task-board',
  standalone: true,
  imports: [SharedModule, StatusPillComponent, EmptyStateComponent, FilterBarComponent],
  template: `
<div class="ad-page">
  <div class="ad-page-header">
    <div>
      <h1 class="ad-h1">{{ 'tasks.board.title' | translate }}</h1>
      <p class="ad-sub">{{ 'tasks.board.subtitle' | translate }}</p>
    </div>
    @if (theaterId) {
      <div class="ad-toolbar">
        <button mat-raised-button color="primary" type="button" (click)="openDialog()"><mat-icon>add_task</mat-icon> {{ 'tasks.board.new' | translate }}</button>
      </div>
    }
  </div>

  @if (!theaterId) {
    <mat-card class="ad-card--pad-0">
      <cl-empty-state icon="theaters" messageKey="opsCommon.pickTheater" hintKey="opsCommon.pickTheaterHint" />
    </mat-card>
  } @else {
    <cl-filter-bar [form]="searchForm" [fields]="filterFields" (filtersChange)="onFilterChange()" />
    <mat-card class="ad-card--pad-0">
      <div class="ad-table-wrap">
        <table class="ad-table">
          <thead>
            <tr>
              <th>{{ 'tasks.fields.title' | translate }}</th>
              <th>{{ 'tasks.fields.assignee' | translate }}</th>
              <th>{{ 'common.status' | translate }}</th>
              <th>{{ 'tasks.fields.dueAt' | translate }}</th>
              <th>{{ 'tasks.fields.createdBy' | translate }}</th>
            </tr>
          </thead>
          <tbody>
            @for (row of rows; track row.id) {
              <tr class="clickable" (click)="openDialog(row)">
                <td><strong>{{ row.title }}</strong></td>
                <td>{{ row.assignedToName }}</td>
                <td><cl-status-pill kind="staffTask" [value]="row.status" /></td>
                <td>{{ row.dueAt | date: 'dd/MM HH:mm' }}</td>
                <td>{{ row.createdByName }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      @if (!rows.length && !loading) {
        <cl-empty-state messageKey="tasks.board.empty" />
      }
      @if (total > 0) {
        <mat-paginator [length]="total" [pageSize]="pageSize" [pageIndex]="pageIndex" [hidePageSize]="true" (page)="onPage($event.pageIndex)"></mat-paginator>
      }
    </mat-card>
  }
</div>
`,
  styles: [`
    .clickable { cursor: pointer; }
    .clickable:hover { background: var(--ml-panel-3, rgba(0, 0, 0, 0.04)); }
  `],
})
export class TaskBoardComponent implements OnInit {
  readonly pageSize = 10;

  filterFields: FilterBarField[] = [
    { key: 'status', type: 'select', labelKey: 'common.status', options: StaffTaskStatusValues.map(s => ({ value: String(s.value), labelKey: s.name })) },
    { key: 'assignedTo', type: 'select', labelKey: 'tasks.fields.assignee', options: [] },
  ];

  searchForm: FormGroup;
  rows: StaffServiceAgent.StaffTaskDTO[] = [];
  staff: StaffServiceAgent.TheaterStaffDTO[] = [];
  total = 0;
  pageIndex = 0;
  loading = false;

  private readonly _workforce = inject(StaffServiceAgent.WorkforceHttpService);
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
    this._workforce.getTasks(StaffServiceAgent.PagingSearchDTO.fromJS({
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

  openDialog(task?: StaffServiceAgent.StaffTaskDTO): void {
    this._dialog.open(TaskDialog, { width: '520px', maxWidth: '95vw', data: { theaterId: this.theaterId, staff: this.staff, task } })
      .afterClosed().subscribe(saved => {
        if (saved) {
          this.load();
        }
      });
  }

  private _loadStaff(theaterId: string): void {
    this._workforce.getTheaterStaff(StaffServiceAgent.GetTheaterStaffRequest.fromJS({ theaterId })).subscribe({
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

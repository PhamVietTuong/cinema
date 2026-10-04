import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { Store } from '@ngrx/store';
import { Subject } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import {
  AuditActionValues,
  EmptyStateComponent,
  FilterBarComponent,
  FilterBarField,
  SharedModule,
  StaffServiceAgent,
  StatusPillComponent,
  hideLoading,
  showException,
  showLoading,
} from 'CinemaLib';
import { TheaterContextService } from '../../../core/theater-context.service';

/** Paged audit trail of sensitive staff actions (overrides, refunds, voids, ...), filterable by action and date. */
@Component({
  selector: 'staff-audit-log',
  standalone: true,
  imports: [SharedModule, StatusPillComponent, EmptyStateComponent, FilterBarComponent],
  template: `
<div class="ad-page">
  <div class="ad-page-header">
    <div>
      <h1 class="ad-h1">{{ 'auditLog.title' | translate }}</h1>
      <p class="ad-sub">{{ 'auditLog.subtitle' | translate }}</p>
    </div>
  </div>

  <cl-filter-bar [form]="searchForm" [fields]="filterFields" (filtersChange)="onFilterChange()" />

  <mat-card class="ad-card--pad-0">
    <div class="ad-table-wrap">
      <table class="ad-table">
        <thead>
          <tr>
            <th>{{ 'common.createdAt' | translate }}</th>
            <th>{{ 'auditLog.col.action' | translate }}</th>
            <th>{{ 'auditLog.col.actor' | translate }}</th>
            <th>{{ 'auditLog.col.approver' | translate }}</th>
            <th>{{ 'auditLog.col.entity' | translate }}</th>
            <th class="num">{{ 'auditLog.col.amount' | translate }}</th>
            <th>{{ 'auditLog.col.reason' | translate }}</th>
          </tr>
        </thead>
        <tbody>
          @for (row of rows; track row.id) {
            <tr>
              <td>{{ row.creationTime | date: 'dd/MM/yyyy HH:mm:ss' }}</td>
              <td><cl-status-pill kind="auditAction" [value]="row.action" /></td>
              <td>{{ row.actorName }}</td>
              <td>{{ row.approverName }}</td>
              <td>{{ row.entityType }}</td>
              <td class="num">{{ row.amount | number: '1.0-0' }}</td>
              <td>{{ row.reason }}</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
    @if (!rows.length && !loading) {
      <cl-empty-state icon="fact_check" messageKey="auditLog.empty" />
    }
    @if (total > 0) {
      <mat-paginator
        [length]="total"
        [pageSize]="pageSize"
        [pageIndex]="pageIndex"
        [hidePageSize]="true"
        (page)="onPage($event.pageIndex)">
      </mat-paginator>
    }
  </mat-card>
</div>
`,
  styles: [`.num { text-align: right; }`],
})
export class AuditLogComponent implements OnInit {
  readonly pageSize = 20;

  readonly filterFields: FilterBarField[] = [
    {
      key: 'action',
      type: 'select',
      labelKey: 'auditLog.col.action',
      options: AuditActionValues.map(a => ({ value: String(a.value), labelKey: a.name })),
    },
    { key: 'from', type: 'date', labelKey: 'auditLog.from' },
    { key: 'to', type: 'date', labelKey: 'auditLog.to' },
  ];

  searchForm: FormGroup;
  rows: StaffServiceAgent.AuditLogDTO[] = [];
  total = 0;
  pageIndex = 0; // 0-based for the paginator
  loading = false;

  private readonly _theaterContext = inject(TheaterContextService);
  private readonly _filterChange$ = new Subject<void>();

  constructor(
    private _report: StaffServiceAgent.StaffReportHttpService,
    private _fb: FormBuilder,
    private _store: Store<any>,
    private _cd: ChangeDetectorRef,
  ) {
    this.searchForm = this._fb.group({ action: [''], from: [''], to: [''] });

    // Reload when an Admin switches the topbar theater.
    effect(() => {
      this._theaterContext.currentTheaterId();
      this.pageIndex = 0;
      this.load();
    });
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
    const filters: { [key: string]: string } = {};
    const theaterId = this._theaterContext.currentTheaterId();
    if (theaterId) {
      filters['theaterId'] = theaterId;
    }
    const raw = this.searchForm.value as Record<string, string>;
    for (const key of Object.keys(raw)) {
      const value = (raw[key] ?? '').toString().trim();
      if (value) {
        filters[key] = value;
      }
    }
    this.loading = true;
    this._store.dispatch(showLoading());
    this._report.getAuditLog(StaffServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: this.pageIndex + 1, pageSize: this.pageSize, filters,
    })).subscribe({
      next: r => {
        this.rows = r.results ?? [];
        this.total = r.totalCount ?? 0;
        this._cd.markForCheck();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this.loading = false;
      this._store.dispatch(hideLoading());
      this._cd.markForCheck();
    });
  }
}

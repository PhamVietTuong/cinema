import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { Store } from '@ngrx/store';
import { Subject } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import {
  EmptyStateComponent, FilterBarComponent, FilterBarField, IncidentCategoryValues, IncidentStatusValues,
  SharedModule, CinemaServiceAgent, StatusPillComponent, incidentCategoryLabel, hideLoading, showException, showLoading,
} from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';
import { ReportIncidentDialog } from './report-incident.dialog';

/** Paged incident list of the current theater with status / category / date filters and a "report" action. */
@Component({
  selector: 'staff-incident-list',
  standalone: true,
  imports: [SharedModule, StatusPillComponent, EmptyStateComponent, FilterBarComponent],
  template: `
<div class="ad-page">
  <div class="ad-page-header">
    <div>
      <h1 class="ad-h1">{{ 'incidents.list.title' | translate }}</h1>
      <p class="ad-sub">{{ 'incidents.list.subtitle' | translate }}</p>
    </div>
    @if (theaterId) {
      <div class="ad-toolbar">
        <button mat-raised-button color="primary" type="button" (click)="report()"><mat-icon>report</mat-icon> {{ 'incidents.list.report' | translate }}</button>
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
              <th>{{ 'incidents.fields.title' | translate }}</th>
              <th>{{ 'incidents.fields.category' | translate }}</th>
              <th>{{ 'incidents.fields.severity' | translate }}</th>
              <th>{{ 'common.status' | translate }}</th>
              <th>{{ 'incidents.fields.location' | translate }}</th>
              <th>{{ 'incidents.fields.reportedBy' | translate }}</th>
              <th>{{ 'common.createdAt' | translate }}</th>
            </tr>
          </thead>
          <tbody>
            @for (row of rows; track row.id) {
              <tr class="clickable" (click)="open(row)">
                <td><strong>{{ row.title }}</strong></td>
                <td>{{ categoryLabel(row.category) | translate }}</td>
                <td><cl-status-pill kind="incidentSeverity" [value]="row.severity" /></td>
                <td><cl-status-pill kind="incident" [value]="row.status" /></td>
                <td>{{ row.roomName }} {{ row.seatLabel }}</td>
                <td>{{ row.reportedByName }}</td>
                <td>{{ row.creationTime | serverUtc | date: 'dd/MM/yyyy HH:mm' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      @if (!rows.length && !loading) {
        <cl-empty-state messageKey="incidents.list.empty" />
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
export class IncidentListComponent implements OnInit {
  readonly pageSize = 10;
  readonly categoryLabel = incidentCategoryLabel;

  readonly filterFields: FilterBarField[] = [
    { key: 'status', type: 'select', labelKey: 'common.status', options: IncidentStatusValues.map(s => ({ value: String(s.value), labelKey: s.name })) },
    { key: 'category', type: 'select', labelKey: 'incidents.fields.category', options: IncidentCategoryValues.map(c => ({ value: String(c.value), labelKey: c.name })) },
    { key: 'from', type: 'date', labelKey: 'opsCommon.from' },
    { key: 'to', type: 'date', labelKey: 'opsCommon.to' },
  ];

  searchForm: FormGroup;
  rows: CinemaServiceAgent.IncidentDTO[] = [];
  total = 0;
  pageIndex = 0;
  loading = false;

  private readonly _ops = inject(CinemaServiceAgent.HttpService);
  private readonly _theaterContext = inject(TheaterContextService);
  private readonly _store = inject(Store);
  private readonly _router = inject(Router);
  private readonly _dialog = inject(MatDialog);
  private readonly _cd = inject(ChangeDetectorRef);
  private readonly _filterChange$ = new Subject<void>();

  constructor(fb: FormBuilder) {
    this.searchForm = fb.group({ status: [''], category: [''], from: [''], to: [''] });
    effect(() => {
      this._theaterContext.currentTheaterId();
      this.pageIndex = 0;
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
    this._ops.getIncidents(CinemaServiceAgent.PagingSearchDTO.fromJS({
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

  report(): void {
    this._dialog.open(ReportIncidentDialog, { width: '560px', maxWidth: '95vw', data: { theaterId: this.theaterId } })
      .afterClosed().subscribe(incident => {
        if (incident?.id) {
          this._router.navigate(['/incidents', incident.id]);
        }
      });
  }

  open(row: CinemaServiceAgent.IncidentDTO): void {
    this._router.navigate(['/incidents', row.id]);
  }
}

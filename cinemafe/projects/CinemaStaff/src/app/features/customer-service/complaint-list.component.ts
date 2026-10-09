import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { Router, RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { Subject } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import {
  ComplaintCategoryValues, ComplaintStatusValues, EmptyStateComponent, FilterBarComponent, FilterBarField, SharedModule, StaffServiceAgent,
  SELLER_ROLES, StatusPillComponent, complaintCategoryLabel, hideLoading, selectCurrentUser, showException, showLoading,
} from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';
import { ComplaintDialogComponent, ComplaintDialogData } from './complaint.dialog';
import { complaintFilters } from './customer-service.logic';

/** Paged complaints of the caller's theaters (an Admin's picked theater when set) with status / category filters. */
@Component({
  selector: 'staff-complaint-list',
  standalone: true,
  imports: [SharedModule, RouterLink, StatusPillComponent, EmptyStateComponent, FilterBarComponent],
  template: `
<div class="ad-page">
  <div class="ad-page-header">
    <div>
      <h1 class="ad-h1">{{ 'customerService.list.title' | translate }}</h1>
      <p class="ad-sub">{{ 'customerService.list.subtitle' | translate }}</p>
    </div>
    <div class="ad-toolbar">
      @if (canLookup) {
        <a mat-stroked-button routerLink="/customer-service/lookup"><mat-icon>person_search</mat-icon> {{ 'customerService.nav.lookup' | translate }}</a>
      }
      <button mat-raised-button color="primary" type="button" (click)="create()">
        <mat-icon>add</mat-icon> {{ 'customerService.complaint.new' | translate }}
      </button>
    </div>
  </div>

  <cl-filter-bar [form]="searchForm" [fields]="filterFields" (filtersChange)="onFilterChange()" />

  <mat-card class="ad-card--pad-0">
    <div class="ad-table-wrap">
      <table class="ad-table">
        <thead>
          <tr>
            <th>{{ 'customerService.complaint.category' | translate }}</th>
            <th>{{ 'customerService.complaint.description' | translate }}</th>
            <th>{{ 'customerService.complaint.customer' | translate }}</th>
            <th>{{ 'customerService.complaint.invoice' | translate }}</th>
            <th>{{ 'common.status' | translate }}</th>
            <th>{{ 'customerService.complaint.assignedTo' | translate }}</th>
            <th>{{ 'common.createdAt' | translate }}</th>
          </tr>
        </thead>
        <tbody>
          @for (row of rows; track row.id) {
            <tr class="clickable" (click)="open(row)">
              <td>{{ categoryLabel(row.category) | translate }}</td>
              <td class="desc">{{ row.description }}</td>
              <td>{{ row.customerName }}</td>
              <td>{{ row.invoiceCode }}</td>
              <td><cl-status-pill kind="complaint" [value]="row.status" /></td>
              <td>{{ row.assignedToName }}</td>
              <td>{{ row.creationTime | serverUtc | date: 'dd/MM/yyyy HH:mm' }}</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
    @if (!rows.length && !loading) {
      <cl-empty-state icon="report_problem" messageKey="customerService.list.empty" />
    }
    @if (total > 0) {
      <mat-paginator [length]="total" [pageSize]="pageSize" [pageIndex]="pageIndex" [hidePageSize]="true" (page)="onPage($event.pageIndex)"></mat-paginator>
    }
  </mat-card>
</div>
`,
  styles: [`
    .clickable { cursor: pointer; }
    .clickable:hover { background: var(--ml-panel-3, rgba(0, 0, 0, 0.04)); }
    .desc { max-width: 320px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  `],
})
export class ComplaintListComponent implements OnInit {
  readonly pageSize = 10;
  readonly categoryLabel = complaintCategoryLabel;

  readonly filterFields: FilterBarField[] = [
    { key: 'status', type: 'select', labelKey: 'common.status', options: ComplaintStatusValues.map(s => ({ value: String(s.value), labelKey: s.name })) },
    { key: 'category', type: 'select', labelKey: 'customerService.complaint.category', options: ComplaintCategoryValues.map(c => ({ value: String(c.value), labelKey: c.name })) },
  ];

  searchForm: FormGroup;
  rows: StaffServiceAgent.ComplaintDTO[] = [];
  total = 0;
  pageIndex = 0;
  loading = false;

  private readonly _api = inject(StaffServiceAgent.CustomerServiceHttpService);
  private readonly _theater = inject(TheaterContextService);
  private readonly _store = inject(Store);
  private readonly _user = this._store.selectSignal(selectCurrentUser);
  private readonly _router = inject(Router);
  private readonly _dialog = inject(MatDialog);
  private readonly _cd = inject(ChangeDetectorRef);
  private readonly _filterChange$ = new Subject<void>();

  constructor(fb: FormBuilder) {
    this.searchForm = fb.group({ status: [''], category: [''] });
    effect(() => {
      this._theater.currentTheaterId();
      this.pageIndex = 0;
      this.load();
    });
  }

  /** Only sellers may open the lookup (a regional manager reaches complaints only). */
  get canLookup(): boolean {
    return SELLER_ROLES.includes(this._user()?.userTypeName ?? '');
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
    // An Admin sees the picked theater (all when none); everyone else is scoped by the API.
    const theaterId = this._theater.isAdmin() ? this._theater.currentTheaterId() : null;
    const filters = complaintFilters(this.searchForm.value as Record<string, unknown>, theaterId);
    this.loading = true;
    this._store.dispatch(showLoading());
    this._api.getComplaints(StaffServiceAgent.PagingSearchDTO.fromJS({ pageIndex: this.pageIndex + 1, pageSize: this.pageSize, filters })).subscribe({
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

  create(): void {
    const data: ComplaintDialogData = { theaterId: this._theater.isAdmin() ? (this._theater.currentTheaterId() ?? undefined) : undefined };
    this._dialog.open<ComplaintDialogComponent, ComplaintDialogData, StaffServiceAgent.ComplaintDTO>(
      ComplaintDialogComponent, { width: '520px', maxWidth: '95vw', data },
    ).afterClosed().subscribe(complaint => {
      if (complaint?.id) {
        this._router.navigate(['/customer-service/complaints', complaint.id]);
      }
    });
  }

  open(row: StaffServiceAgent.ComplaintDTO): void {
    this._router.navigate(['/customer-service/complaints', row.id]);
  }
}

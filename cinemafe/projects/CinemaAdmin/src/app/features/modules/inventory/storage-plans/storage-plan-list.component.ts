import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { Subject } from 'rxjs';
import { debounceTime, take } from 'rxjs/operators';
import {
  CinemaServiceAgent,
  SharedModule,
  StoragePlanStatusValues,
  storagePlanStatusLabel,
  storagePlanStatusPillClass,
  selectIsAdmin,
  showException,
  hideLoading,
  showLoading,
} from 'CinemaLib';

/** Paged list of storage (restock) plans with status / code / theater filters. */
@Component({
  selector: 'app-storage-plan-list',
  standalone: true,
  imports: [SharedModule],
  template: `
<div class="ad-page">
  <div class="ad-page-header">
    <div>
      <h1 class="ad-h1">{{ 'storagePlans.list.title' | translate }}</h1>
      <p class="ad-sub">{{ 'storagePlans.list.subtitle' | translate }}</p>
    </div>
    <div class="ad-toolbar">
      <button mat-raised-button color="primary" (click)="openNew()">
        <mat-icon>add</mat-icon> {{ 'storagePlans.list.newPlan' | translate }}
      </button>
    </div>
  </div>

  <mat-card class="ad-filter-card">
    <mat-card-content>
      <h3 class="ad-card-title">{{ 'common.filters' | translate }}</h3>
      <form [formGroup]="searchForm" class="ad-filter-grid">
        <mat-form-field appearance="outline">
          <mat-label>{{ 'storagePlans.list.code' | translate }}</mat-label>
          <input matInput formControlName="keyword" (input)="onFilterChange()">
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>{{ 'common.status' | translate }}</mat-label>
          <mat-select formControlName="status" (selectionChange)="onFilterChange()">
            <mat-option value="">{{ 'common.all' | translate }}</mat-option>
            @for (s of statuses; track s.value) {
              <mat-option [value]="'' + s.value">{{ s.name | translate }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        @if (isAdmin) {
          <mat-form-field appearance="outline">
            <mat-label>{{ 'storagePlans.list.theater' | translate }}</mat-label>
            <mat-select formControlName="theaterId" (selectionChange)="onFilterChange()">
              <mat-option value="">{{ 'common.all' | translate }}</mat-option>
              @for (t of theaters; track t.id) {
                <mat-option [value]="t.id">{{ t.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        }
      </form>
    </mat-card-content>
  </mat-card>

  <mat-card class="ad-card--pad-0">
    <div class="ad-table-wrap">
      <table class="ad-table">
        <thead>
          <tr>
            <th>{{ 'storagePlans.list.code' | translate }}</th>
            @if (isAdmin) {
              <th>{{ 'storagePlans.list.theater' | translate }}</th>
            }
            <th>{{ 'common.status' | translate }}</th>
            <th>{{ 'storagePlans.list.targetDate' | translate }}</th>
            <th>{{ 'storagePlans.list.supplier' | translate }}</th>
            <th class="num">{{ 'storagePlans.list.itemCount' | translate }}</th>
            <th class="num">{{ 'storagePlans.list.totalQuantity' | translate }}</th>
            <th>{{ 'storagePlans.list.createdBy' | translate }}</th>
            <th>{{ 'common.createdAt' | translate }}</th>
          </tr>
        </thead>
        <tbody>
          @for (row of rows; track row.id) {
            <tr class="clickable" (click)="open(row)">
              <td><strong>{{ row.code }}</strong></td>
              @if (isAdmin) {
                <td>{{ row.theaterName }}</td>
              }
              <td><span class="ad-pill" [ngClass]="pillClass(row.status)">{{ statusLabel(row.status) | translate }}</span></td>
              <td>{{ row.targetDate | date: 'dd/MM/yyyy' }}</td>
              <td>{{ row.supplier }}</td>
              <td class="num">{{ row.itemCount }}</td>
              <td class="num">{{ row.totalPlannedQuantity }}</td>
              <td>{{ row.createdByName }}</td>
              <td>{{ row.creationTime | date: 'dd/MM/yyyy HH:mm' }}</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
    @if (!rows.length && !loading) {
      <div class="ad-empty"><mat-icon>inventory_2</mat-icon><p>{{ 'storagePlans.list.empty' | translate }}</p></div>
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
  styles: [`
    .clickable { cursor: pointer; }
    .clickable:hover { background: var(--ml-panel-3, rgba(0, 0, 0, 0.04)); }
    .num { text-align: right; }
  `],
})
export class StoragePlanListComponent implements OnInit {
  readonly statuses = StoragePlanStatusValues;
  readonly statusLabel = storagePlanStatusLabel;
  readonly pageSize = 10;

  searchForm: FormGroup;
  rows: CinemaServiceAgent.StoragePlanListItemDTO[] = [];
  theaters: CinemaServiceAgent.TheaterDTO[] = [];
  total = 0;
  pageIndex = 0; // 0-based for the paginator
  loading = false;
  isAdmin = false;

  private readonly _filterChange$ = new Subject<void>();

  constructor(
    private _cinema: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _router: Router,
    private _store: Store<any>,
    private _cd: ChangeDetectorRef,
  ) {
    this.searchForm = this._fb.group({ keyword: [''], status: [''], theaterId: [''] });
  }

  ngOnInit(): void {
    this._filterChange$.pipe(debounceTime(300)).subscribe(() => {
      this.pageIndex = 0;
      this.load();
    });
    this._store.select(selectIsAdmin).pipe(take(1)).subscribe(isAdmin => {
      this.isAdmin = isAdmin;
      if (isAdmin) {
        this._cinema.getTheaters(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200 }))
          .subscribe(r => {
            this.theaters = r.results ?? [];
            this._cd.markForCheck();
          });
      }
    });
    this.load();
  }

  pillClass(status?: CinemaServiceAgent.StoragePlanStatus): string {
    return storagePlanStatusPillClass(status);
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
    const raw = this.searchForm.value as Record<string, string>;
    for (const key of Object.keys(raw)) {
      const value = (raw[key] ?? '').toString().trim();
      if (value) {
        filters[key] = value;
      }
    }
    this.loading = true;
    this._store.dispatch(showLoading());
    this._cinema.getStoragePlans(CinemaServiceAgent.PagingSearchDTO.fromJS({
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

  openNew(): void {
    this._router.navigate(['/storage-plans', 'new']);
  }

  open(row: CinemaServiceAgent.StoragePlanListItemDTO): void {
    this._router.navigate(['/storage-plans', row.id]);
  }
}

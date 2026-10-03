import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { MatDialog } from '@angular/material/dialog';
import { NgxDatatableModule } from '@swimlane/ngx-datatable';
import { TranslateService } from '@ngx-translate/core';
import {
  CinemaServiceAgent, SharedModule,
  BaseTableComponent, TablePage, TableSearchCriteria,
  selectIsAdmin, selectIsStockApprover, selectUserTheaterId,
  showLoading, hideLoading, showSuccess, showException,
} from 'CinemaLib';
import { StockSettingsDialog } from './stock-settings.dialog';
import { StockMovementDialog } from './stock-movement.dialog';
import { StockCountDialog } from './stock-count.dialog';
import { StockHistoryDialog } from './stock-history.dialog';

type Item = CinemaServiceAgent.InventoryItemDTO;

/** Warehouse inventory: tracked stock per theater with settings, manual movements, stock count and history. */
@Component({
  selector: 'app-inventory-list',
  standalone: true,
  imports: [SharedModule, NgxDatatableModule],
  template: `
    <div class="ad-page">
      <div class="ad-page-header">
        <div>
          <h1 class="ad-h1">{{ 'inventory.title' | translate }}</h1>
          <p class="ad-sub">{{ 'inventory.subtitle' | translate }}</p>
        </div>
        <div class="ad-toolbar">
          <button mat-raised-button color="primary" *ngIf="planTheaterId" (click)="createPlan()">
            <mat-icon>playlist_add</mat-icon> {{ 'inventory.createPlan' | translate }}
          </button>
        </div>
      </div>

      <mat-card class="ad-filter-card">
        <mat-card-content>
          <h3 class="ad-card-title">{{ 'common.filters' | translate }}</h3>
          <form [formGroup]="searchForm" class="ad-filter-grid">
            <mat-form-field appearance="outline" *ngIf="isAdmin">
              <mat-label>{{ 'inventory.filters.theater' | translate }}</mat-label>
              <mat-select formControlName="theaterId" (selectionChange)="onFilterChange()">
                <mat-option value="">{{ 'inventory.filters.allTheaters' | translate }}</mat-option>
                <mat-option *ngFor="let t of theaters" [value]="t.id">{{ t.name }}</mat-option>
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>{{ 'inventory.filters.keyword' | translate }}</mat-label>
              <input matInput formControlName="keyword" (input)="onFilterChange()">
            </mat-form-field>
            <div class="inv-toggles">
              <mat-slide-toggle formControlName="trackedOnly" (change)="onFilterChange()">{{ 'inventory.filters.trackedOnly' | translate }}</mat-slide-toggle>
              <mat-slide-toggle formControlName="lowStock" (change)="onFilterChange()">{{ 'inventory.filters.lowStockOnly' | translate }}</mat-slide-toggle>
            </div>
          </form>
        </mat-card-content>
      </mat-card>

      <mat-card class="ad-card--pad-0">
        <div class="ad-table-wrap ad-desktop-only">
          <ngx-datatable
            class="material ad-datatable"
            [rows]="pageRows"
            [columnMode]="'force'"
            [headerHeight]="44"
            [footerHeight]="50"
            [rowHeight]="'auto'"
            [externalPaging]="true"
            [externalSorting]="true"
            [count]="total"
            [offset]="pageOffset"
            [limit]="pageSize"
            [loadingIndicator]="loadingIndicator"
            [messages]="{ emptyMessage: 'inventory.empty' | translate, selectedMessage: '' }"
            (page)="onChangePage($event)"
            (sort)="onSort($any($event))">

            <ngx-datatable-column [name]="'common.name' | translate" prop="name" [sortable]="true">
              <ng-template let-row="row" ngx-datatable-cell-template><strong>{{ row.name }}</strong></ng-template>
            </ngx-datatable-column>

            <ngx-datatable-column [name]="'inventory.columns.stock' | translate" prop="quantityOnHand" [sortable]="true" [width]="130" [canAutoResize]="false">
              <ng-template let-row="row" ngx-datatable-cell-template>{{ row.trackInventory ? row.quantityOnHand : '-' }}</ng-template>
            </ngx-datatable-column>

            <ngx-datatable-column [name]="'inventory.columns.threshold' | translate" prop="lowStockThreshold" [sortable]="false" [width]="130" [canAutoResize]="false">
              <ng-template let-row="row" ngx-datatable-cell-template>{{ row.trackInventory ? row.lowStockThreshold : '-' }}</ng-template>
            </ngx-datatable-column>

            <ngx-datatable-column [name]="'inventory.columns.target' | translate" prop="targetStockLevel" [sortable]="false" [width]="130" [canAutoResize]="false">
              <ng-template let-row="row" ngx-datatable-cell-template>{{ row.trackInventory ? row.targetStockLevel : '-' }}</ng-template>
            </ngx-datatable-column>

            <ngx-datatable-column [name]="'common.status' | translate" prop="status" [sortable]="false" [width]="150" [canAutoResize]="false">
              <ng-template let-row="row" ngx-datatable-cell-template>
                <span class="ad-pill" [ngClass]="statusClass(row)">{{ statusKey(row) | translate }}</span>
              </ng-template>
            </ngx-datatable-column>

            <ngx-datatable-column [name]="'common.actions' | translate" [sortable]="false" [width]="190" [canAutoResize]="false">
              <ng-template let-row="row" ngx-datatable-cell-template>
                <div class="ad-td-actions ad-td-actions--mat">
                  <ng-container *ngIf="row.trackInventory">
                    <button mat-icon-button (click)="openMovement(row)" [matTooltip]="'inventory.actions.movement' | translate"><mat-icon>swap_vert</mat-icon></button>
                    <button mat-icon-button (click)="openCount(row)" [matTooltip]="'inventory.actions.count' | translate"><mat-icon>fact_check</mat-icon></button>
                    <button mat-icon-button (click)="openHistory(row)" [matTooltip]="'inventory.actions.history' | translate"><mat-icon>history</mat-icon></button>
                  </ng-container>
                  <button mat-icon-button *ngIf="isApprover" (click)="openSettings(row)" [matTooltip]="'inventory.actions.settings' | translate"><mat-icon>tune</mat-icon></button>
                </div>
              </ng-template>
            </ngx-datatable-column>

            <ngx-datatable-footer>
              <ng-template ngx-datatable-footer-template>
                <div class="page-count">{{ 'common.totalEntries' | translate }} {{ total }}</div>
                <ngx-datatable-pager *ngIf="total > pageSize"></ngx-datatable-pager>
              </ng-template>
            </ngx-datatable-footer>
          </ngx-datatable>
        </div>

        <div class="ad-mobile-only">
          <mat-card class="ad-mobile-card" *ngFor="let row of pageRows" appearance="outlined">
            <mat-card-content>
              <div class="ad-mobile-card-title">
                <strong>{{ row.name }}</strong>
                <span class="ad-pill" [ngClass]="statusClass(row)">{{ statusKey(row) | translate }}</span>
              </div>
              <div class="ad-mobile-card-row"><span>{{ 'inventory.columns.stock' | translate }}</span><span>{{ row.trackInventory ? row.quantityOnHand : '-' }}</span></div>
              <div class="ad-mobile-card-row"><span>{{ 'inventory.columns.threshold' | translate }}</span><span>{{ row.trackInventory ? row.lowStockThreshold : '-' }}</span></div>
              <div class="ad-mobile-card-row"><span>{{ 'inventory.columns.target' | translate }}</span><span>{{ row.trackInventory ? row.targetStockLevel : '-' }}</span></div>
              <div class="ad-mobile-card-actions">
                <ng-container *ngIf="row.trackInventory">
                  <button mat-icon-button (click)="openMovement(row)" [matTooltip]="'inventory.actions.movement' | translate"><mat-icon>swap_vert</mat-icon></button>
                  <button mat-icon-button (click)="openCount(row)" [matTooltip]="'inventory.actions.count' | translate"><mat-icon>fact_check</mat-icon></button>
                  <button mat-icon-button (click)="openHistory(row)" [matTooltip]="'inventory.actions.history' | translate"><mat-icon>history</mat-icon></button>
                </ng-container>
                <button mat-icon-button *ngIf="isApprover" (click)="openSettings(row)" [matTooltip]="'inventory.actions.settings' | translate"><mat-icon>tune</mat-icon></button>
              </div>
            </mat-card-content>
          </mat-card>
          <div *ngIf="!pageRows.length" class="ad-empty"><mat-icon>inventory_2</mat-icon><p>{{ 'inventory.empty' | translate }}</p></div>

          <mat-paginator
            *ngIf="total > 0"
            [length]="total"
            [pageSize]="pageSize"
            [pageIndex]="pageOffset"
            [hidePageSize]="true"
            (page)="onChangePage({ pageSize: $event.pageSize, offset: $event.pageIndex })">
          </mat-paginator>
        </div>
      </mat-card>
    </div>
  `,
  styles: [`
    .inv-toggles { display: flex; flex-wrap: wrap; gap: 8px 24px; align-items: center; padding-bottom: 8px; }
  `],
})
export class InventoryListComponent extends BaseTableComponent<Item> implements OnInit {
  isAdmin = false;
  isApprover = false;
  userTheaterId: string | null = null;
  theaters: CinemaServiceAgent.TheaterDTO[] = [];

  constructor(
    cd: ChangeDetectorRef,
    fb: FormBuilder,
    router: Router,
    store: Store<any>,
    private _svc: CinemaServiceAgent.HttpService,
    private _dialog: MatDialog,
    private _translate: TranslateService,
  ) {
    super(cd, fb, router, store);
    this.defaultSearchFormValue = { theaterId: '', keyword: '', trackedOnly: false, lowStock: false };
  }

  protected override _createSearchForm(): void {
    this.searchForm = this._formBuilder.group({ theaterId: [''], keyword: [''], trackedOnly: [false], lowStock: [false] });
  }

  /** The theater a low-stock plan would be created for: the chosen one (Admin) or the user's own. */
  get planTheaterId(): string {
    if (this.isAdmin) {
      return String(this.searchForm.get('theaterId')?.value ?? '');
    }
    return this.userTheaterId ?? '';
  }

  override ngOnInit(): void {
    this._store.select(selectIsAdmin).subscribe(v => { this.isAdmin = !!v; this._cd.markForCheck(); });
    this._store.select(selectIsStockApprover).subscribe(v => { this.isApprover = !!v; this._cd.markForCheck(); });
    this._store.select(selectUserTheaterId).subscribe(v => { this.userTheaterId = v ?? null; this._cd.markForCheck(); });
    if (this.isAdmin) {
      this._loadTheaters();
    }
    super.ngOnInit();
  }

  private _loadTheaters(): void {
    this._svc.getTheaters(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200, filters: {} })).subscribe({
      next: r => {
        this.theaters = r.results ?? [];
        this._cd.markForCheck();
      },
      error: error => this._store.dispatch(showException({ error })),
    });
  }

  /** Switch-off toggles are not sent: the API reads "false" as a real filter value. */
  protected override _activeFilters(): Record<string, string> {
    const out = super._activeFilters();
    for (const key of ['trackedOnly', 'lowStock']) {
      if (out[key] === 'false') {
        delete out[key];
      }
    }
    return out;
  }

  protected _search(criteria: TableSearchCriteria): Observable<TablePage<Item>> {
    return this._svc.getInventory(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: criteria.pageIndex,
      pageSize: criteria.pageSize,
      filters: criteria.filters,
      sort: criteria.sort ? { field: criteria.sort.field, ascending: criteria.sort.ascending } : undefined,
    })).pipe(map(r => ({ results: r.results ?? [], totalCount: r.totalCount })));
  }

  statusKey(row: Item): string {
    if (!row.trackInventory) {
      return 'inventory.status.untracked';
    }
    if (row.isOutOfStock) {
      return 'inventory.status.outOfStock';
    }
    if (row.isLowStock) {
      return 'inventory.status.low';
    }
    return 'inventory.status.ok';
  }

  statusClass(row: Item): string {
    if (!row.trackInventory) {
      return 'ad-pill--neutral';
    }
    if (row.isOutOfStock) {
      return 'ad-pill--danger';
    }
    if (row.isLowStock) {
      return 'ad-pill--warn';
    }
    return 'ad-pill--success';
  }

  openSettings(row: Item): void {
    this._dialog.open(StockSettingsDialog, { width: '520px', maxWidth: '95vw', data: { item: row } })
      .afterClosed().subscribe(saved => { this._afterAction(saved); });
  }

  openMovement(row: Item): void {
    this._dialog.open(StockMovementDialog, { width: '520px', maxWidth: '95vw', data: { item: row } })
      .afterClosed().subscribe(saved => { this._afterAction(saved); });
  }

  openCount(row: Item): void {
    this._dialog.open(StockCountDialog, { width: '480px', maxWidth: '95vw', data: { item: row } })
      .afterClosed().subscribe(saved => { this._afterAction(saved); });
  }

  openHistory(row: Item): void {
    this._dialog.open(StockHistoryDialog, { width: '820px', maxWidth: '95vw', data: { item: row } });
  }

  private _afterAction(saved?: boolean): void {
    if (saved) {
      this.triggerSearch();
    }
  }

  createPlan(): void {
    const theaterId = this.planTheaterId;
    if (!theaterId) {
      return;
    }
    this._store.dispatch(showLoading());
    this._svc.createStoragePlanFromLowStock(CinemaServiceAgent.CreatePlanFromLowStockRequest.fromJS({ theaterId })).subscribe({
      next: plan => {
        this._store.dispatch(showSuccess({ message: this._translate.instant('inventory.toast.planCreated') }));
        this._router.navigate(['/storage-plans', plan.id]);
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }
}

import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { Observable, of } from 'rxjs';
import { map } from 'rxjs/operators';
import { MatDialog } from '@angular/material/dialog';
import { NgxDatatableModule } from '@swimlane/ngx-datatable';
import { TranslateService } from '@ngx-translate/core';
import {
  CinemaServiceAgent, SharedModule,
  BaseTableComponent, TablePage, TableSearchCriteria,
  EmptyStateComponent, FilterBarComponent, FilterBarField, StatusPillComponent, stockLevelOf,
  selectIsStockApprover,
  showLoading, hideLoading, showSuccess, showException,
} from 'CinemaLib';
import { TheaterContextService } from '../../../core/theater-context.service';
import { StockSettingsDialog } from './stock-settings.dialog';
import { StockMovementDialog } from './stock-movement.dialog';
import { StockCountDialog } from './stock-count.dialog';
import { StockHistoryDialog } from './stock-history.dialog';

type Item = CinemaServiceAgent.InventoryItemDTO;

/** Warehouse inventory: tracked stock of the current theater with settings, manual movements, stock count and history. */
@Component({
  selector: 'staff-inventory-list',
  standalone: true,
  imports: [SharedModule, NgxDatatableModule, StatusPillComponent, EmptyStateComponent, FilterBarComponent],
  template: `
    <div class="ad-page">
      <div class="ad-page-header">
        <div>
          <h1 class="ad-h1">{{ 'inventory.title' | translate }}</h1>
          <p class="ad-sub">{{ 'inventory.subtitle' | translate }}</p>
        </div>
        <div class="ad-toolbar">
          <button mat-raised-button color="primary" *ngIf="theaterId" (click)="createPlan()">
            <mat-icon>playlist_add</mat-icon> {{ 'inventory.createPlan' | translate }}
          </button>
        </div>
      </div>

      <cl-filter-bar [form]="searchForm" [fields]="filterFields" (filtersChange)="onFilterChange()" />

      <mat-card class="ad-card--pad-0" *ngIf="!theaterId">
        <cl-empty-state icon="theaters" messageKey="warehouse.pickTheater" hintKey="warehouse.pickTheaterHint" />
      </mat-card>

      <mat-card class="ad-card--pad-0" *ngIf="theaterId">
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
                <cl-status-pill kind="stockLevel" [value]="stockLevel(row)" />
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
                <cl-status-pill kind="stockLevel" [value]="stockLevel(row)" />
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
          <cl-empty-state *ngIf="!pageRows.length" messageKey="inventory.empty" />

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
})
export class InventoryListComponent extends BaseTableComponent<Item> implements OnInit {
  isApprover = false;

  /** Filter controls rendered by the shared filter bar. */
  readonly filterFields: FilterBarField[] = [
    { key: 'keyword', type: 'text', labelKey: 'inventory.filters.keyword' },
    { key: 'trackedOnly', type: 'toggle', labelKey: 'inventory.filters.trackedOnly' },
    { key: 'lowStock', type: 'toggle', labelKey: 'inventory.filters.lowStockOnly' },
  ];

  private readonly _theaterContext = inject(TheaterContextService);
  /** Theater seen by the previous effect run; undefined until the first run, so the initial load isn't doubled. */
  private _seenTheaterId: string | null | undefined = undefined;

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
    this.defaultSearchFormValue = { keyword: '', trackedOnly: false, lowStock: false };

    // Reload when an Admin switches the topbar theater.
    effect(() => {
      const theaterId = this._theaterContext.currentTheaterId();
      if (this._seenTheaterId !== undefined && this._seenTheaterId !== theaterId) {
        this.pageOffset = 0;
        this.triggerSearch();
      }
      this._seenTheaterId = theaterId;
    });
  }

  protected override _createSearchForm(): void {
    this.searchForm = this._formBuilder.group({ keyword: [''], trackedOnly: [false], lowStock: [false] });
  }

  /** Theater the page is scoped to; empty while an Admin has not picked one. */
  get theaterId(): string {
    return this._theaterContext.currentTheaterId() ?? '';
  }

  override ngOnInit(): void {
    this._store.select(selectIsStockApprover).subscribe(v => { this.isApprover = !!v; this._cd.markForCheck(); });
    super.ngOnInit();
  }

  protected override _extraFilters(): Record<string, unknown> {
    return { theaterId: this.theaterId };
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
    if (!this.theaterId) {
      return of({ results: [], totalCount: 0 });
    }
    return this._svc.getInventory(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: criteria.pageIndex,
      pageSize: criteria.pageSize,
      filters: criteria.filters,
      sort: criteria.sort ? { field: criteria.sort.field, ascending: criteria.sort.ascending } : undefined,
    })).pipe(map(r => ({ results: r.results ?? [], totalCount: r.totalCount })));
  }

  stockLevel(row: Item): string {
    return stockLevelOf(row);
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
    const theaterId = this.theaterId;
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

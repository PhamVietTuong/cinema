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
  templateUrl: './inventory-list.component.html',
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

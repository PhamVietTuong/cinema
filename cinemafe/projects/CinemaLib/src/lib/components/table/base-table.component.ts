import { ChangeDetectorRef, Directive, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { FormBuilder, FormGroup } from '@angular/forms';
import { Observable, Subject } from 'rxjs';
import { debounceTime, take, takeUntil } from 'rxjs/operators';
import { Store, select } from '@ngrx/store';
import { BaseReactiveComponent } from './base-reactive.component';
import { saveSearchState } from '../../store/actions/search.actions';
import { selectSearchState } from '../../store/selectors/search.selector';

/** Shape of the paged result returned by a list endpoint. */
export interface TablePage<T> {
  results?: T[] | undefined;
  totalCount?: number | undefined;
}

/** Per-route criteria snapshot persisted so a list restores its page/filters/sort on return. */
export interface TableSearchCriteria {
  pageSize: number;
  pageIndex: number;
  filters: Record<string, unknown>;
  sort: { field: string; ascending: boolean } | null;
}

/**
 * Shared list/search/pagination base for admin data-grid pages, backed by ngx-datatable.
 * CRUD (create/edit/delete/dialog) is NOT provided here — each subclass declares that itself,
 * the same way the members were declared before this class existed.
 *
 * Search-state persistence reuses the pre-existing `SearchActions.saveSearchState` /
 * `selectSearchState` (in `lib/store/actions|selectors/search.*`) — a flat, url-keyed map at
 * the store's `searchState` root, not a `createFeatureSelector` slice. Register `searchReducer`
 * under the `searchState` key in the consuming app's `StoreModule.forRoot(...)`.
 *
 * Ordering gotcha: `_createSearchForm()` runs from inside `super()`, i.e. BEFORE the subclass's
 * own field initializers and constructor body run. It must only reference `this._formBuilder` —
 * never a field the subclass declares.
 */
@Directive()
export abstract class BaseTableComponent<TRow = any> extends BaseReactiveComponent implements OnInit {
  pageRows: TRow[] = [];
  total = 0;
  pageSize = 10;
  pageOffset = 0; // 0-based, matches ngx-datatable's (page) event
  searchForm!: FormGroup;
  loadingIndicator = true;
  isFirstSearch = true;
  sort: { field: string; ascending: boolean } | null = null;
  defaultSearchFormValue: Record<string, unknown> = {};

  /** Last known full url->criteria map from the store, merged into on save so other routes' saved state isn't lost. */
  private _searchStateByUrl: Record<string, TableSearchCriteria> = {};

  private readonly _filterChange$ = new Subject<void>();

  constructor(
    protected _cd: ChangeDetectorRef,
    protected _formBuilder: FormBuilder,
    protected _router: Router,
    protected _store: Store<any>,
  ) {
    super();
    this._createSearchForm();
  }

  ngOnInit(): void {
    this._filterChange$.pipe(debounceTime(300), takeUntil(this.ngDestroyed$)).subscribe(() => {
      this.isFirstSearch = false;
      this.pageOffset = 0;
      this.triggerSearch();
    });

    this._store.pipe(select(selectSearchState), take(1)).subscribe((byUrl: Record<string, TableSearchCriteria> | null) => {
      this._searchStateByUrl = byUrl ?? {};
      const criteria = this._searchStateByUrl[this._searchStateKey()];
      if (criteria) {
        this.pageSize = criteria.pageSize;
        this.pageOffset = Math.max(0, criteria.pageIndex - 1);
        this.sort = criteria.sort;
        this.searchForm.patchValue({ ...criteria.filters }, { emitEvent: false });
      }
      this.triggerSearch();
    });
  }

  /** Fired (debounced) by per-column filter inputs bound to `searchForm` controls. */
  onFilterChange(): void {
    this._filterChange$.next();
  }

  /** Overridable: builds the filter form. Runs inside `super()` — only use `this._formBuilder`. */
  protected _createSearchForm(): void {
    this.searchForm = this._formBuilder.group({});
  }

  /** Overridable: extra, non-form-bound filters to merge in (e.g. a parent-scoped id). */
  protected _extraFilters(): Record<string, unknown> {
    return {};
  }

  /** Drops empty/blank filter values so only active filters reach the server. */
  protected _activeFilters(): Record<string, string> {
    const out: Record<string, string> = {};
    const raw = this.searchForm.value as Record<string, unknown>;
    for (const key of Object.keys(raw)) {
      const value = raw[key];
      if (value === null || value === undefined) {
        continue;
      }
      const asString = String(value).trim();
      if (asString) {
        out[key] = asString;
      }
    }
    return out;
  }

  /** Overridable: the key search state is saved/restored under (default: the current route URL). */
  protected _searchStateKey(): string {
    return this._router.url;
  }

  onChangePage(pageInfo: { pageSize?: number; offset?: number }): void {
    this.isFirstSearch = false;
    this.pageSize = pageInfo.pageSize ?? this.pageSize;
    this.pageOffset = pageInfo.offset ?? 0;
    this.triggerSearch();
  }

  onSort(event: { sorts: { prop: string; dir: string }[] }): void {
    const s = event.sorts[0];
    this.sort = s ? { field: s.prop, ascending: s.dir === 'asc' } : null;
    this.triggerSearch();
  }

  triggerSearch(): void {
    const criteria: TableSearchCriteria = {
      pageSize: this.pageSize,
      pageIndex: this.pageOffset + 1, // server-side PagingSearchDTO.PageIndex is 1-based
      filters: { ...this._activeFilters(), ...this._extraFilters() },
      sort: this.sort,
    };

    this.loadingIndicator = true;
    this._cd.markForCheck();
    this._search(criteria).pipe(takeUntil(this.ngDestroyed$)).subscribe(data => {
      if (Array.isArray(data)) {
        this.pageRows = data;
        this.total = data.length;
      } else {
        this.pageRows = data.results ?? [];
        this.total = data.totalCount ?? 0;
      }
      this._cd.markForCheck();
    }).add(() => {
      this._searchStateByUrl = { ...this._searchStateByUrl, [this._searchStateKey()]: criteria };
      this._store.dispatch(saveSearchState({ searchState: this._searchStateByUrl }));
      this.loadingIndicator = false;
      this._cd.markForCheck();
    });
  }

  resetSearchForm(): void {
    this.searchForm.reset(this.defaultSearchFormValue, { emitEvent: false });
    this.pageOffset = 0;
    this.triggerSearch();
  }

  /** Fetches one page. Return either the paged shape ({results, totalCount}) or a plain array. */
  protected abstract _search(criteria: TableSearchCriteria): Observable<TablePage<TRow> | TRow[]>;
}

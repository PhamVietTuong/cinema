import { ChangeDetectorRef, Component, Input } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { Observable, forkJoin, of } from 'rxjs';
import { map, switchMap } from 'rxjs/operators';
import { MatDialog } from '@angular/material/dialog';
import {
  CinemaServiceAgent,
  BaseTableComponent, TablePage, TableSearchCriteria,
  DialogService,
  showLoading, hideLoading, showSuccess, showException,
} from 'CinemaLib';
import { FoodAndDrinkDialog } from './food-and-drink.dialog';

type Dto = CinemaServiceAgent.FoodAndDrinkDTO;

/** Combo management scoped to a single theater (combos are food items flagged isCombo). */
@Component({
  selector: 'app-theater-combos',
  standalone: false,
  templateUrl: './theater-combos.component.html',
  styleUrls: ['./theater-catalog-tab.scss'],
})
export class TheaterCombosComponent extends BaseTableComponent<Dto> {
  @Input({ required: true }) theaterId!: string;

  /** Components summary ("1 × Popcorn + 1 × Coke") keyed by combo id, for the combos on the current page. */
  summaries: Record<string, string> = {};

  constructor(
    cd: ChangeDetectorRef,
    fb: FormBuilder,
    router: Router,
    store: Store<any>,
    private _svc: CinemaServiceAgent.HttpService,
    private _dialog: MatDialog,
    private _dialogService: DialogService,
  ) {
    super(cd, fb, router, store);
  }

  protected override _createSearchForm(): void {
    this.searchForm = this._formBuilder.group({});
  }

  protected _search(criteria: TableSearchCriteria): Observable<TablePage<Dto>> {
    // Load the theater's items (API max page size 200), keep only combos and page client-side.
    return this._svc.getFoodAndDrinks(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: 1, pageSize: 200, filters: criteria.filters,
    })).pipe(
      switchMap(res => {
        const combos = (res.results ?? []).filter(f => f.isCombo === true);
        const start = (criteria.pageIndex - 1) * criteria.pageSize;
        const pageCombos = combos.slice(start, start + criteria.pageSize);
        const page: TablePage<Dto> = { results: pageCombos, totalCount: combos.length };
        if (!pageCombos.length) {
          this.summaries = {};
          return of(page);
        }
        // One call per combo shown on the page (at most pageSize).
        return forkJoin(pageCombos.map(c => this._svc.getComboComponents(c.id as string))).pipe(
          map(lists => {
            const summaries: Record<string, string> = {};
            pageCombos.forEach((c, i) => {
              summaries[c.id as string] = (lists[i] ?? []).map(x => `${x.quantity} × ${x.name}`).join(' + ');
            });
            this.summaries = summaries;
            return page;
          }),
        );
      }),
    );
  }

  protected override _extraFilters(): Record<string, unknown> {
    return { theaterId: this.theaterId };
  }

  protected override _searchStateKey(): string {
    return this._router.url + '#combos';
  }

  summaryOf(row: Dto): string {
    return (row.id ? this.summaries[row.id] : '') || '—';
  }

  openCreate(): void {
    this._dialog.open(FoodAndDrinkDialog, { width: '600px', data: { theaterId: this.theaterId, foodAndDrink: null, presetCombo: true } })
      .afterClosed().subscribe(saved => {
        if (saved) {
          this.triggerSearch();
        }
      });
  }

  edit(item: Dto): void {
    this._dialog.open(FoodAndDrinkDialog, { width: '600px', data: { theaterId: this.theaterId, foodAndDrink: item } })
      .afterClosed().subscribe(saved => {
        if (saved) {
          this.triggerSearch();
        }
      });
  }

  delete(id?: string): void {
    if (!id) {
      return;
    }
    this._dialogService.openConfirmDialog({ message: 'common.confirmDelete' })
      .afterClosed().subscribe(confirmed => {
        if (confirmed) {
          this._deleteConfirmed(id);
        }
      });
  }

  private _deleteConfirmed(id: string): void {
    this._store.dispatch(showLoading());
    this._svc.deleteFoodAndDrink(id).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({}));
        this.triggerSearch();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }
}

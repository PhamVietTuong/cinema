import { ChangeDetectorRef, Component, Input } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { MatDialog } from '@angular/material/dialog';
import {
  CinemaServiceAgent,
  BaseTableComponent, TablePage, TableSearchCriteria,
  DialogService,
  showLoading, hideLoading, showSuccess, showException,
} from 'CinemaLib';
import { FoodAndDrinkDialog } from './food-and-drink.dialog';

type Dto = CinemaServiceAgent.FoodAndDrinkDTO;

/** Food & drink management scoped to a single theater. */
@Component({
  selector: 'app-theater-food',
  standalone: false,
  templateUrl: './theater-food.component.html',
  styleUrls: ['./theater-catalog-tab.scss'],
})
export class TheaterFoodComponent extends BaseTableComponent<Dto> {
  @Input({ required: true }) theaterId!: string;

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
    // Combos live in their own tab: load the theater's items (API max page size 200), drop combos and page client-side.
    return this._svc.getFoodAndDrinks(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: 1, pageSize: 200, filters: criteria.filters,
    })).pipe(map(res => {
      const foods = (res.results ?? []).filter(f => !f.isCombo);
      const start = (criteria.pageIndex - 1) * criteria.pageSize;
      return { results: foods.slice(start, start + criteria.pageSize), totalCount: foods.length };
    }));
  }

  protected override _extraFilters(): Record<string, unknown> {
    return { theaterId: this.theaterId };
  }

  protected override _searchStateKey(): string {
    return this._router.url + '#food';
  }

  openCreate(): void {
    this._dialog.open(FoodAndDrinkDialog, { width: '600px', data: { theaterId: this.theaterId, foodAndDrink: null } })
      .afterClosed().subscribe(saved => { if (saved) { this.triggerSearch(); } });
  }

  edit(item: Dto): void {
    this._dialog.open(FoodAndDrinkDialog, { width: '600px', data: { theaterId: this.theaterId, foodAndDrink: item } })
      .afterClosed().subscribe(saved => { if (saved) { this.triggerSearch(); } });
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

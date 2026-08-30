import { ChangeDetectorRef, Component } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { Observable } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';
import { TranslateService } from '@ngx-translate/core';
import {
  CinemaServiceAgent,
  BaseTableComponent, TablePage, TableSearchCriteria,
  DialogService,
  showLoading, hideLoading, showSuccess, showException,
} from 'CinemaLib';
import { DiscountDialog } from './discount.dialog';

type Dto = CinemaServiceAgent.DiscountDTO;

@Component({
  selector: 'app-discounts',
  standalone: false,
  templateUrl: './discounts.component.html',
})
export class DiscountsManagementComponent extends BaseTableComponent {
  discountTypes: CinemaServiceAgent.DiscountTypeDTO[] = [];
  theaters: CinemaServiceAgent.TheaterDTO[] = [];
  movies: CinemaServiceAgent.MovieDTO[] = [];

  constructor(
    cd: ChangeDetectorRef,
    fb: FormBuilder,
    router: Router,
    store: Store<any>,
    private _svc: CinemaServiceAgent.HttpService,
    private _dialog: MatDialog,
    private _dialogService: DialogService,
    private _translate: TranslateService,
  ) {
    super(cd, fb, router, store);
  }

  override ngOnInit(): void {
    super.ngOnInit();

    this._svc.getDiscountTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200 }))
      .subscribe(r => { this.discountTypes = r.results ?? []; this._cd.markForCheck(); });
    this._svc.getTheaters(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200 }))
      .subscribe(r => { this.theaters = r.results ?? []; this._cd.markForCheck(); });
    this._svc.getMovies(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200 }))
      .subscribe(r => { this.movies = r.results ?? []; this._cd.markForCheck(); });
  }

  protected override _createSearchForm(): void {
    this.searchForm = this._formBuilder.group({ code: [''], percent: [''] });
  }

  protected _search(criteria: TableSearchCriteria): Observable<TablePage<Dto>> {
    return this._svc.getDiscounts(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: criteria.pageIndex, pageSize: criteria.pageSize, filters: criteria.filters,
    }));
  }

  openCreate(): void {
    this._dialog.open(DiscountDialog, { width: '640px', data: { discount: null } })
      .afterClosed().subscribe(saved => { if (saved) { this.triggerSearch(); } });
  }

  edit(item: Dto): void {
    this._dialog.open(DiscountDialog, { width: '640px', data: { discount: item } })
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

  typeName(id?: string): string {
    return this.discountTypes.find(t => t.id === id)?.name ?? '—';
  }

  scopeLabel(x: Dto): string {
    if (x.applyToAllTheaters) {
      return this._translate.instant('discounts.systemWide');
    }
    return this._translate.instant('discounts.nTheaters', { n: x.theaterIds?.length ?? 0 });
  }

  private _deleteConfirmed(id: string): void {
    this._store.dispatch(showLoading());
    this._svc.deleteDiscount(id).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({}));
        this.triggerSearch();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }
}

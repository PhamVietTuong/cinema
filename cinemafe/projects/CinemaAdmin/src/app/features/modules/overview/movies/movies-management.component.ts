import { ChangeDetectorRef, Component } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { Observable } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';
import {
  CinemaServiceAgent,
  BaseTableComponent, TablePage, TableSearchCriteria,
  DialogService,
  showLoading, hideLoading, showSuccess, showException,
} from 'CinemaLib';
import { MovieDialog } from './movie.dialog';

type Dto = CinemaServiceAgent.MovieDTO;

@Component({
  selector: 'app-movies-management',
  standalone: false,
  templateUrl: './movies-management.component.html',
})
export class MoviesManagementComponent extends BaseTableComponent {
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
    this.searchForm = this._formBuilder.group({ title: [''], director: [''] });
  }

  protected _search(criteria: TableSearchCriteria): Observable<TablePage<Dto>> {
    return this._svc.getMovies(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: criteria.pageIndex, pageSize: criteria.pageSize, filters: criteria.filters,
    }));
  }

  openCreate(): void {
    this._dialog.open(MovieDialog, { width: '640px', data: { movie: null } })
      .afterClosed().subscribe(saved => { if (saved) { this.triggerSearch(); } });
  }

  edit(item: Dto): void {
    this._dialog.open(MovieDialog, { width: '640px', data: { movie: item } })
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
    this._svc.deleteMovie(id).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({}));
        this.triggerSearch();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }
}

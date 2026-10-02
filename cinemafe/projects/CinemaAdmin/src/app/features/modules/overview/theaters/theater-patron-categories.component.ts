import { ChangeDetectorRef, Component, Input, OnInit } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Store } from '@ngrx/store';
import {
  CinemaServiceAgent,
  DialogService,
  showLoading, hideLoading, showSuccess, showException,
} from 'CinemaLib';
import { PatronCategoryDialog } from './patron-category.dialog';

type Dto = CinemaServiceAgent.PatronCategoryDTO;

/** Patron-category pricing management scoped to a single theater. Each PatronCategory row is an
 * independent (Name, seat kind) pricing entry — rows are never grouped, merged, or cascaded by
 * Name; editing/deleting one row never affects another. The dataset is small (a handful of rows),
 * so it's loaded in full rather than paged. */
@Component({
  selector: 'app-theater-patron-categories',
  standalone: false,
  templateUrl: './theater-patron-categories.component.html',
  styleUrls: ['./theater-catalog-tab.scss'],
})
export class TheaterPatronCategoriesComponent implements OnInit {
  @Input({ required: true }) theaterId!: string;

  seatTypes: CinemaServiceAgent.SeatTypeDTO[] = [];
  rows: Dto[] = [];
  loading = false;

  constructor(
    private _cd: ChangeDetectorRef,
    private _svc: CinemaServiceAgent.HttpService,
    private _dialog: MatDialog,
    private _dialogService: DialogService,
    private _store: Store<any>,
  ) {}

  ngOnInit(): void {
    this._svc.getSeatTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: 1, pageSize: 100, filters: { theaterId: this.theaterId },
    })).subscribe(r => {
      this.seatTypes = (r.results ?? []).sort((a, b) => (a.kind ?? 0) - (b.kind ?? 0));
      this._cd.markForCheck();
    });
    this._load();
  }

  private _load(): void {
    this.loading = true;
    this._cd.markForCheck();
    this._svc.getPatronCategoriesByTheater(this.theaterId).subscribe({
      next: rows => {
        this.rows = (rows ?? []).sort((a, b) =>
          (a.name ?? '').localeCompare(b.name ?? '') || (a.kind ?? 0) - (b.kind ?? 0));
        this.loading = false;
        this._cd.markForCheck();
      },
      error: () => {
        this.loading = false;
        this._cd.markForCheck();
      },
    });
  }

  openCreate(): void {
    this._dialog.open(PatronCategoryDialog, { width: '560px', data: { theaterId: this.theaterId, seatTypes: this.seatTypes, category: null } })
      .afterClosed().subscribe(saved => { if (saved) { this._load(); } });
  }

  edit(row: Dto): void {
    this._dialog.open(PatronCategoryDialog, { width: '560px', data: { theaterId: this.theaterId, seatTypes: this.seatTypes, category: row } })
      .afterClosed().subscribe(saved => { if (saved) { this._load(); } });
  }

  delete(row: Dto): void {
    this._dialogService.openConfirmDialog({ message: 'common.confirmDelete' })
      .afterClosed().subscribe(confirmed => {
        if (confirmed) {
          this._deleteConfirmed(row.id!);
        }
      });
  }

  private _deleteConfirmed(id: string): void {
    this._store.dispatch(showLoading());
    this._svc.deletePatronCategory(id).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({}));
        this._load();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }
}

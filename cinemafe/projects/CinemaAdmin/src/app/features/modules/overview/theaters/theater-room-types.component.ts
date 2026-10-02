import { ChangeDetectorRef, Component, Input } from '@angular/core';
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
import { RoomTypeDialog } from './room-type.dialog';
import { RoomTypePricesDialog } from './room-type-prices.dialog';

type Dto = CinemaServiceAgent.RoomTypeDTO;

/**
 * Room-class management scoped to a single theater (Standard/IMAX/4DX/Lagom…). A class carries the
 * base-price tier plus whether its rooms can screen 3D and what a 3D screening adds per ticket.
 */
@Component({
  selector: 'app-theater-room-types',
  standalone: false,
  templateUrl: './theater-room-types.component.html',
  styleUrls: ['./theater-catalog-tab.scss'],
})
export class TheaterRoomTypesComponent extends BaseTableComponent {
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
    return this._svc.getRoomTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: criteria.pageIndex, pageSize: criteria.pageSize, filters: criteria.filters,
    }));
  }

  protected override _extraFilters(): Record<string, unknown> {
    return { theaterId: this.theaterId };
  }

  protected override _searchStateKey(): string {
    return this._router.url + '#roomTypes';
  }

  openCreate(): void {
    this._dialog.open(RoomTypeDialog, { width: '560px', data: { theaterId: this.theaterId, roomType: null } })
      .afterClosed().subscribe(saved => { if (saved) { this.triggerSearch(); } });
  }

  edit(item: Dto): void {
    this._dialog.open(RoomTypeDialog, { width: '560px', data: { theaterId: this.theaterId, roomType: item } })
      .afterClosed().subscribe(saved => { if (saved) { this.triggerSearch(); } });
  }

  openPrices(item: Dto): void {
    this._dialog.open(RoomTypePricesDialog, { width: '620px', data: { theaterId: this.theaterId, roomType: item } });
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
    this._svc.deleteRoomType(id).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({}));
        this.triggerSearch();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }
}

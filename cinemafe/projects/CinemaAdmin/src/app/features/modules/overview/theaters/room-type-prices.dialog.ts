import { ChangeDetectorRef, Component, Inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CinemaServiceAgent, showLoading, hideLoading, showSuccess, showException } from 'CinemaLib';

export interface RoomTypePricesDialogData {
  theaterId: string;
  roomType: CinemaServiceAgent.RoomTypeDTO;
}

const _emptyGuid = '00000000-0000-0000-0000-000000000000';

interface PriceRow {
  patronCategoryId: string;
  patronCategoryName: string;
  seatTypeName: string;
  defaultPrice: number;
  /** null = no override, falls back to defaultPrice. A blank input clears the override. */
  overridePrice: number | null;
}

/** Lets an admin override a RoomType's per-(PatronCategory, SeatKind) price — e.g. IMAX charges more
 * for Adult/Standard than the theater-wide default. Blank = no override (uses the default).
 * Resolves `true` on save, `false` on cancel. */
@Component({
  selector: 'app-room-type-prices-dialog',
  standalone: false,
  templateUrl: './room-type-prices.dialog.html',
  styleUrls: ['./theater-catalog-tab.scss'],
})
export class RoomTypePricesDialog {
  readonly roomType: CinemaServiceAgent.RoomTypeDTO;
  rows: PriceRow[] = [];
  loading = true;
  saving = false;

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _cd: ChangeDetectorRef,
    private _store: Store<any>,
    private _dialogRef: MatDialogRef<RoomTypePricesDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) private _data: RoomTypePricesDialogData,
  ) {
    this.roomType = _data.roomType;
    this._load();
  }

  private _load(): void {
    this.loading = true;
    this._svc.getRoomTypePatronCategoryPrices(this.roomType.id!).subscribe({
      next: dtos => {
        this.rows = (dtos ?? []).map(d => ({
          patronCategoryId: d.patronCategoryId!,
          patronCategoryName: `${d.patronCategoryName} (${d.seatTypeName})`,
          seatTypeName: d.seatTypeName ?? '',
          defaultPrice: d.defaultPrice ?? 0,
          overridePrice: d.id && d.id !== _emptyGuid ? (d.price ?? 0) : null,
        }));
        this.loading = false;
        this._cd.markForCheck();
      },
      error: () => { this.loading = false; this._cd.markForCheck(); },
    });
  }

  clearOverride(row: PriceRow): void {
    row.overridePrice = null;
  }

  save(): void {
    this.saving = true;
    const request = CinemaServiceAgent.SaveRoomTypePatronCategoryPricesRequest.fromJS({
      roomTypeId: this.roomType.id,
      items: this.rows.map(r => ({ patronCategoryId: r.patronCategoryId, price: r.overridePrice })),
    });
    this._store.dispatch(showLoading());
    this._svc.saveRoomTypePatronCategoryPrices(request).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({}));
        this.saving = false;
        this._dialogRef.close(true);
      },
      error: error => {
        this._store.dispatch(showException({ error }));
        this.saving = false;
        this._cd.markForCheck();
      },
    }).add(() => this._store.dispatch(hideLoading()));
  }

  cancel(): void {
    this._dialogRef.close(false);
  }
}

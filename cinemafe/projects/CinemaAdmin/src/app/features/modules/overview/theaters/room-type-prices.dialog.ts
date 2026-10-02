import { ChangeDetectorRef, Component, Inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CinemaServiceAgent, showLoading, hideLoading, showSuccess, showException } from 'CinemaLib';

export interface RoomTypePricesDialogData {
  theaterId: string;
  roomType: CinemaServiceAgent.RoomTypeDTO;
}

interface PriceRow {
  patronCategoryId: string;
  patronCategoryName: string;
  seatTypeName: string;
  defaultPrice: number;
  price: number;
}

/** Lets an admin curate a RoomType's patron-category allow-list: only categories added to the list
 * are offered in this RoomType, each at its own price (defaults to the theater-wide price, editable).
 * Categories are added one at a time via a dropdown (avoids showing every theater category at once)
 * and removed with a delete button. Resolves `true` on save, `false` on cancel. */
@Component({
  selector: 'app-room-type-prices-dialog',
  standalone: false,
  templateUrl: './room-type-prices.dialog.html',
  styleUrls: ['./theater-catalog-tab.scss'],
})
export class RoomTypePricesDialog {
  readonly roomType: CinemaServiceAgent.RoomTypeDTO;
  /** Every theater category, used as the source for the "add category" dropdown. */
  private _allCategories: Omit<PriceRow, 'price'>[] = [];
  /** Category ids that were already included when the dialog loaded — anything removed from
   * `includedRows` that started in this set must still be sent to the server (as excluded) so the
   * row actually gets deleted; SaveAsync only touches categories present in the request. */
  private _originalIncludedIds = new Set<string>();
  includedRows: PriceRow[] = [];
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

  get hasNoIncluded(): boolean {
    return this._allCategories.length > 0 && this.includedRows.length === 0;
  }

  /** Categories not yet in the list, offered by the "add category" dropdown. */
  get availableToAdd(): Omit<PriceRow, 'price'>[] {
    const includedIds = new Set(this.includedRows.map(r => r.patronCategoryId));
    return this._allCategories.filter(c => !includedIds.has(c.patronCategoryId));
  }

  private _load(): void {
    this.loading = true;
    this._svc.getRoomTypePatronCategoryPrices(this.roomType.id!).subscribe({
      next: dtos => {
        const all = (dtos ?? []).map(d => ({
          patronCategoryId: d.patronCategoryId!,
          patronCategoryName: `${d.patronCategoryName} (${d.seatTypeName})`,
          seatTypeName: d.seatTypeName ?? '',
          defaultPrice: d.defaultPrice ?? 0,
        }));
        this._allCategories = all;
        this.includedRows = (dtos ?? [])
          .filter(d => d.isIncluded)
          .map(d => ({
            patronCategoryId: d.patronCategoryId!,
            patronCategoryName: `${d.patronCategoryName} (${d.seatTypeName})`,
            seatTypeName: d.seatTypeName ?? '',
            defaultPrice: d.defaultPrice ?? 0,
            price: d.price ?? d.defaultPrice ?? 0,
          }));
        this._originalIncludedIds = new Set(this.includedRows.map(r => r.patronCategoryId));
        this.loading = false;
        this._cd.markForCheck();
      },
      error: () => { this.loading = false; this._cd.markForCheck(); },
    });
  }

  addCategory(patronCategoryId: string): void {
    const category = this._allCategories.find(c => c.patronCategoryId === patronCategoryId);
    if (!category) {
      return;
    }
    this.includedRows.push({ ...category, price: category.defaultPrice });
  }

  removeCategory(row: PriceRow): void {
    this.includedRows = this.includedRows.filter(r => r.patronCategoryId !== row.patronCategoryId);
  }

  save(): void {
    this.saving = true;
    const includedIds = new Set(this.includedRows.map(r => r.patronCategoryId));
    const removedIds = [...this._originalIncludedIds].filter(id => !includedIds.has(id));
    const items = [
      ...this.includedRows.map(r => ({ patronCategoryId: r.patronCategoryId, included: true, price: r.price })),
      ...removedIds.map(id => ({ patronCategoryId: id, included: false, price: null })),
    ];
    const request = CinemaServiceAgent.SaveRoomTypePatronCategoryPricesRequest.fromJS({
      roomTypeId: this.roomType.id,
      items,
    });
    this._store.dispatch(showLoading());
    this._svc.saveRoomTypePatronCategoryPrices(request).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({}));
        this.saving = false;
        // Flush the "saving" binding synchronously before closing — MatDialog's close() triggers
        // its own change-detection pass on this still-attached view, and without this the pending
        // saving=false mutation trips NG0100 (ExpressionChangedAfterItHasBeenCheckedError).
        this._cd.detectChanges();
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

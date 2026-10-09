import { ChangeDetectorRef, Component, Inject, OnInit } from '@angular/core';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import {
  CinemaServiceAgent, EmptyStateComponent, SharedModule, StockMovementTypeValues, StockReasonCodeValues,
  showException,
} from 'CinemaLib';

export interface StockHistoryDialogData {
  item: CinemaServiceAgent.InventoryItemDTO;
}

/** Paged stock movement history of one item (read-only). */
@Component({
  selector: 'app-stock-history-dialog',
  standalone: true,
  imports: [SharedModule, EmptyStateComponent],
  templateUrl: './stock-history.dialog.html',
  styleUrl: './stock-history.dialog.scss',
})
export class StockHistoryDialog implements OnInit {
  rows: CinemaServiceAgent.StockMovementDTO[] = [];
  total = 0;
  pageSize = 10;
  pageIndex = 0;
  loading = false;

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _store: Store<any>,
    private _cdr: ChangeDetectorRef,
    private _dialogRef: MatDialogRef<StockHistoryDialog>,
    @Inject(MAT_DIALOG_DATA) public _data: StockHistoryDialogData,
  ) {}

  ngOnInit(): void {
    this._load();
  }

  typeLabel(type?: CinemaServiceAgent.StockMovementType): string {
    return StockMovementTypeValues.find(t => t.value === type)?.name ?? '';
  }

  reasonLabel(code?: CinemaServiceAgent.StockReasonCode): string {
    return StockReasonCodeValues.find(r => r.value === code)?.name ?? '';
  }

  onPage(index: number): void {
    this.pageIndex = index;
    this._load();
  }

  private _load(): void {
    this.loading = true;
    this._cdr.markForCheck();
    this._svc.getStockMovements(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: this.pageIndex + 1,
      pageSize: this.pageSize,
      filters: { foodAndDrinkId: this._data.item.id },
    })).subscribe({
      next: res => {
        this.rows = res.results ?? [];
        this.total = res.totalCount ?? 0;
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this.loading = false;
      this._cdr.markForCheck();
    });
  }

  close(): void {
    this._dialogRef.close();
  }
}

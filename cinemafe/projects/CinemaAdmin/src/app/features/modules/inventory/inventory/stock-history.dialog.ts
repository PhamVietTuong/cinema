import { ChangeDetectorRef, Component, Inject, OnInit } from '@angular/core';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import {
  CinemaServiceAgent, SharedModule, StockMovementTypeValues, StockReasonCodeValues,
  showException,
} from 'CinemaLib';

export interface StockHistoryDialogData {
  item: CinemaServiceAgent.InventoryItemDTO;
}

/** Paged stock movement history of one item (read-only). */
@Component({
  selector: 'app-stock-history-dialog',
  standalone: true,
  imports: [SharedModule],
  template: `
    <div mat-dialog-title class="dialog-title">{{ 'inventory.history.title' | translate }}: {{ _data.item.name }}</div>
    <mat-dialog-content>
      <div class="hist-wrap">
        <table class="ad-table" *ngIf="rows.length">
          <thead>
            <tr>
              <th>{{ 'inventory.history.date' | translate }}</th>
              <th>{{ 'inventory.history.type' | translate }}</th>
              <th class="num">{{ 'inventory.history.quantity' | translate }}</th>
              <th>{{ 'inventory.history.reason' | translate }}</th>
              <th>{{ 'inventory.history.reference' | translate }}</th>
              <th>{{ 'inventory.history.user' | translate }}</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let m of rows">
              <td>{{ m.creationTime | date: 'dd/MM/yyyy HH:mm' }}</td>
              <td>{{ typeLabel(m.type) | translate }}</td>
              <td class="num"><strong [class.qty-pos]="(m.quantity ?? 0) > 0" [class.qty-neg]="(m.quantity ?? 0) < 0">{{ (m.quantity ?? 0) > 0 ? '+' : '' }}{{ m.quantity }}</strong></td>
              <td>
                <span *ngIf="m.reasonCode !== undefined && m.reasonCode !== null">{{ reasonLabel(m.reasonCode) | translate }}</span>
                <div class="hist-note" *ngIf="m.reason">{{ m.reason }}</div>
              </td>
              <td>{{ m.invoiceCode || m.storagePlanCode }}</td>
              <td>{{ m.userName }}</td>
            </tr>
          </tbody>
        </table>
        <div *ngIf="!rows.length && !loading" class="ad-empty"><mat-icon>history</mat-icon><p>{{ 'inventory.history.empty' | translate }}</p></div>
        <div *ngIf="loading" class="ad-empty"><mat-spinner diameter="28"></mat-spinner></div>
      </div>
      <mat-paginator
        *ngIf="total > pageSize"
        [length]="total"
        [pageSize]="pageSize"
        [pageIndex]="pageIndex"
        [hidePageSize]="true"
        (page)="onPage($event.pageIndex)">
      </mat-paginator>
    </mat-dialog-content>
    <div mat-dialog-actions class="dialog-actions">
      <button mat-raised-button type="button" (click)="close()">{{ 'common.close' | translate }}</button>
    </div>
    <button mat-icon-button type="button" class="dialog-close-btn" (click)="close()"><mat-icon>close</mat-icon></button>
  `,
  styles: [`
    .hist-wrap { overflow-x: auto; }
    .num { text-align: right; }
    .qty-pos { color: #2e7d32; }
    .qty-neg { color: #c62828; }
    .hist-note { font-size: 12px; opacity: 0.75; }
  `],
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

import { Component, Inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { TranslateService } from '@ngx-translate/core';
import { CinemaServiceAgent, SharedModule, showLoading, hideLoading, showSuccess, showException } from 'CinemaLib';

export interface StockCountDialogData {
  item: CinemaServiceAgent.InventoryItemDTO;
}

/** Physical stock count: records the difference between the counted and the system quantity. Resolves `true` on save. */
@Component({
  selector: 'app-stock-count-dialog',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './stock-count.dialog.html',
  styleUrl: './stock-count.dialog.scss',
})
export class StockCountDialog {
  form: FormGroup;

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    fb: FormBuilder,
    private _store: Store<any>,
    private _translate: TranslateService,
    private _dialogRef: MatDialogRef<StockCountDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) public _data: StockCountDialogData,
  ) {
    this.form = fb.group({
      countedQuantity: [null as number | null, [Validators.required, Validators.min(0), Validators.max(100000)]],
      note: [''],
    });
  }

  /** Counted minus on-hand, or null until a valid number is typed. */
  get difference(): number | null {
    const v = this.form.get('countedQuantity')?.value;
    if (v === null || v === undefined || v === '' || isNaN(Number(v))) {
      return null;
    }
    return Number(v) - (this._data.item.quantityOnHand ?? 0);
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    const note = (v.note ?? '').trim();
    this._store.dispatch(showLoading());
    this._svc.recordStockCount(CinemaServiceAgent.RecordStockCountRequest.fromJS({
      foodAndDrinkId: this._data.item.id,
      countedQuantity: Number(v.countedQuantity),
      note: note || null,
    })).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({ message: this._translate.instant('inventory.toast.countSaved') }));
        this._dialogRef.close(true);
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }

  cancel(): void {
    this._dialogRef.close(false);
  }
}

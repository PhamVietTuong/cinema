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
  template: `
    <div mat-dialog-title class="dialog-title">{{ 'inventory.count.title' | translate }}: {{ _data.item.name }}</div>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <div class="dlg-grid">
          <p class="dlg-hint">{{ 'inventory.count.current' | translate }}: <strong>{{ _data.item.quantityOnHand }}</strong></p>

          <div class="dlg-row">
            <div class="dlg-field">
              <label class="dlg-label" for="sc-counted">{{ 'inventory.count.counted' | translate }}</label>
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <input matInput id="sc-counted" type="number" min="0" max="100000" step="1" formControlName="countedQuantity">
                <mat-error>{{ 'inventory.errors.range' | translate }}</mat-error>
              </mat-form-field>
            </div>
          </div>

          <p class="dlg-hint" *ngIf="difference !== null">
            {{ 'inventory.count.difference' | translate }}:
            <strong [class.qty-pos]="difference > 0" [class.qty-neg]="difference < 0">{{ difference > 0 ? '+' : '' }}{{ difference }}</strong>
          </p>

          <div class="dlg-row">
            <div class="dlg-field">
              <label class="dlg-label" for="sc-note">{{ 'inventory.movement.note' | translate }}</label>
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <textarea matInput id="sc-note" rows="2" maxlength="500" formControlName="note"></textarea>
              </mat-form-field>
            </div>
          </div>
        </div>
      </mat-dialog-content>
      <div mat-dialog-actions class="dialog-actions">
        <button mat-raised-button type="button" (click)="cancel()">{{ 'common.cancel' | translate }}</button>
        <button mat-raised-button color="primary" type="submit">{{ 'common.save' | translate }}</button>
      </div>
    </form>
    <button mat-icon-button type="button" class="dialog-close-btn" (click)="cancel()"><mat-icon>close</mat-icon></button>
  `,
  styles: [`
    .qty-pos { color: #2e7d32; }
    .qty-neg { color: #c62828; }
  `],
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

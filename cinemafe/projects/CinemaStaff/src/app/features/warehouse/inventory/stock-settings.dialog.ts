import { Component, Inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { TranslateService } from '@ngx-translate/core';
import { CinemaServiceAgent, SharedModule, showLoading, hideLoading, showSuccess, showException } from 'CinemaLib';

export interface StockSettingsDialogData {
  item: CinemaServiceAgent.InventoryItemDTO;
}

/** Track on/off, low-stock threshold, target level and (when switching on) opening quantity. Resolves `true` on save. */
@Component({
  selector: 'app-stock-settings-dialog',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './stock-settings.dialog.html',
})
export class StockSettingsDialog {
  form: FormGroup;

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    fb: FormBuilder,
    private _store: Store<any>,
    private _translate: TranslateService,
    private _dialogRef: MatDialogRef<StockSettingsDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) public _data: StockSettingsDialogData,
  ) {
    const item = _data.item;
    this.form = fb.group({
      trackInventory: [!!item.trackInventory],
      lowStockThreshold: [item.lowStockThreshold ?? 0, [Validators.required, Validators.min(0), Validators.max(100000)]],
      targetStockLevel: [item.targetStockLevel ?? 0, [Validators.required, Validators.min(0), Validators.max(100000)]],
      openingQuantity: [null as number | null],
    });
  }

  /** The opening quantity is only asked for when tracking is being switched on for an untracked item. */
  get needsOpening(): boolean {
    return !this._data.item.trackInventory && !!this.form.get('trackInventory')?.value;
  }

  save(): void {
    const v = this.form.value;
    const opening = this.form.get('openingQuantity');
    opening?.setValidators(this.needsOpening ? [Validators.required, Validators.min(0), Validators.max(100000)] : []);
    opening?.updateValueAndValidity();
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    this._store.dispatch(showLoading());
    this._svc.updateInventorySettings(CinemaServiceAgent.UpdateInventorySettingsRequest.fromJS({
      foodAndDrinkId: this._data.item.id,
      trackInventory: !!v.trackInventory,
      lowStockThreshold: Number(v.lowStockThreshold),
      targetStockLevel: Number(v.targetStockLevel),
      openingQuantity: this.needsOpening ? Number(v.openingQuantity) : null,
    })).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({ message: this._translate.instant('inventory.toast.settingsSaved') }));
        this._dialogRef.close(true);
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }

  cancel(): void {
    this._dialogRef.close(false);
  }
}

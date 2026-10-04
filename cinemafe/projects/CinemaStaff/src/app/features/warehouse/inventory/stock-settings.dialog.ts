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
  template: `
    <div mat-dialog-title class="dialog-title">{{ 'inventory.settings.title' | translate }}: {{ _data.item.name }}</div>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <div class="dlg-grid">
          <div class="dlg-switch">
            <span class="dlg-switch-label" id="ss-track-lbl">{{ 'inventory.settings.track' | translate }}</span>
            <mat-slide-toggle formControlName="trackInventory" aria-labelledby="ss-track-lbl"></mat-slide-toggle>
          </div>
          <p class="dlg-hint">{{ 'inventory.settings.trackHint' | translate }}</p>

          <div class="dlg-row dlg-row--2">
            <div class="dlg-field">
              <label class="dlg-label" for="ss-threshold">{{ 'inventory.settings.threshold' | translate }}</label>
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <input matInput id="ss-threshold" type="number" min="0" max="100000" formControlName="lowStockThreshold">
                <mat-error>{{ 'inventory.errors.range' | translate }}</mat-error>
              </mat-form-field>
              <p class="dlg-hint">{{ 'inventory.settings.thresholdHint' | translate }}</p>
            </div>
            <div class="dlg-field">
              <label class="dlg-label" for="ss-target">{{ 'inventory.settings.target' | translate }}</label>
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <input matInput id="ss-target" type="number" min="0" max="100000" formControlName="targetStockLevel">
                <mat-error>{{ 'inventory.errors.range' | translate }}</mat-error>
              </mat-form-field>
              <p class="dlg-hint">{{ 'inventory.settings.targetHint' | translate }}</p>
            </div>
          </div>

          <div class="dlg-row" *ngIf="needsOpening">
            <div class="dlg-field">
              <label class="dlg-label" for="ss-opening">{{ 'inventory.settings.opening' | translate }}</label>
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <input matInput id="ss-opening" type="number" min="0" max="100000" formControlName="openingQuantity">
                <mat-error>{{ 'inventory.errors.openingRequired' | translate }}</mat-error>
              </mat-form-field>
              <p class="dlg-hint">{{ 'inventory.settings.openingHint' | translate }}</p>
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

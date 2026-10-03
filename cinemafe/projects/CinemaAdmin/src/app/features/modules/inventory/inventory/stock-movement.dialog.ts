import { Component, Inject } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, ValidationErrors, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { TranslateService } from '@ngx-translate/core';
import {
  CinemaServiceAgent, SharedModule, StockReasonCodeValues,
  showLoading, hideLoading, showSuccess, showException,
} from 'CinemaLib';

export interface StockMovementDialogData {
  item: CinemaServiceAgent.InventoryItemDTO;
}

type MovementType = CinemaServiceAgent.StockMovementType;

/** Manual stock Adjust (signed delta) or Waste (positive quantity) with a mandatory reason. Resolves `true` on save. */
@Component({
  selector: 'app-stock-movement-dialog',
  standalone: true,
  imports: [SharedModule],
  template: `
    <div mat-dialog-title class="dialog-title">{{ 'inventory.movement.title' | translate }}: {{ _data.item.name }}</div>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <div class="dlg-grid">
          <p class="dlg-hint">{{ 'inventory.movement.onHand' | translate }}: <strong>{{ _data.item.quantityOnHand }}</strong></p>

          <div class="dlg-row dlg-row--2">
            <div class="dlg-field">
              <label class="dlg-label">{{ 'inventory.movement.type' | translate }}</label>
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-select formControlName="type">
                  <mat-option *ngFor="let t of types" [value]="t.value">{{ t.name | translate }}</mat-option>
                </mat-select>
              </mat-form-field>
            </div>
            <div class="dlg-field">
              <label class="dlg-label" for="sm-qty">{{ 'inventory.movement.quantity' | translate }}</label>
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <input matInput id="sm-qty" type="number" step="1" formControlName="quantity">
                <mat-error *ngIf="form.controls['quantity'].hasError('required') || form.controls['quantity'].hasError('zero')">{{ 'inventory.errors.nonZero' | translate }}</mat-error>
                <mat-error *ngIf="form.controls['quantity'].hasError('positive')">{{ 'inventory.errors.positive' | translate }}</mat-error>
                <mat-error *ngIf="form.controls['quantity'].hasError('exceeds')">{{ 'inventory.errors.exceedsOnHand' | translate }}</mat-error>
              </mat-form-field>
            </div>
          </div>
          <p class="dlg-hint">{{ (isWaste ? 'inventory.movement.wasteHint' : 'inventory.movement.adjustHint') | translate }}</p>

          <div class="dlg-row">
            <div class="dlg-field">
              <label class="dlg-label">{{ 'inventory.movement.reason' | translate }}</label>
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <mat-select formControlName="reasonCode">
                  <mat-option *ngFor="let r of reasons" [value]="r.value">{{ r.name | translate }}</mat-option>
                </mat-select>
                <mat-error>{{ 'common.required' | translate }}</mat-error>
              </mat-form-field>
            </div>
          </div>

          <div class="dlg-row">
            <div class="dlg-field">
              <label class="dlg-label" for="sm-note">{{ 'inventory.movement.note' | translate }}</label>
              <mat-form-field appearance="outline" subscriptSizing="dynamic">
                <textarea matInput id="sm-note" rows="2" maxlength="500" formControlName="note"></textarea>
                <mat-error>{{ 'inventory.errors.noteRequired' | translate }}</mat-error>
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
})
export class StockMovementDialog {
  readonly types = [
    { value: CinemaServiceAgent.StockMovementType.Adjust, name: 'warehouse.movementType.adjust' },
    { value: CinemaServiceAgent.StockMovementType.Waste, name: 'warehouse.movementType.waste' },
  ];
  /** OpeningBalance is system-only. */
  readonly reasons = StockReasonCodeValues.filter(r => r.value !== CinemaServiceAgent.StockReasonCode.OpeningBalance);
  form: FormGroup;

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    fb: FormBuilder,
    private _store: Store<any>,
    private _translate: TranslateService,
    private _dialogRef: MatDialogRef<StockMovementDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) public _data: StockMovementDialogData,
  ) {
    this.form = fb.group({
      type: [CinemaServiceAgent.StockMovementType.Adjust as MovementType, Validators.required],
      quantity: [null as number | null, [Validators.required, (c: AbstractControl) => this._quantityValidator(c)]],
      reasonCode: [null as CinemaServiceAgent.StockReasonCode | null, Validators.required],
      note: [''],
    });
    this.form.get('type')?.valueChanges.subscribe(() => this.form.get('quantity')?.updateValueAndValidity());
    this.form.get('reasonCode')?.valueChanges.subscribe(() => this._syncNoteValidator());
  }

  get isWaste(): boolean {
    return this.form.get('type')?.value === CinemaServiceAgent.StockMovementType.Waste;
  }

  private _quantityValidator(c: AbstractControl): ValidationErrors | null {
    const value = c.value;
    if (value === null || value === undefined || value === '') {
      return null;
    }
    const n = Number(value);
    if (!Number.isInteger(n) || n === 0) {
      return { zero: true };
    }
    if (this.isWaste) {
      if (n < 0) {
        return { positive: true };
      }
      if (n > (this._data.item.quantityOnHand ?? 0)) {
        return { exceeds: true };
      }
    }
    return null;
  }

  private _syncNoteValidator(): void {
    const note = this.form.get('note');
    const needsNote = this.form.get('reasonCode')?.value === CinemaServiceAgent.StockReasonCode.Other;
    note?.setValidators(needsNote ? [Validators.required, Validators.pattern(/\S/)] : []);
    note?.updateValueAndValidity();
  }

  save(): void {
    this._syncNoteValidator();
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    const note = (v.note ?? '').trim();
    this._store.dispatch(showLoading());
    this._svc.recordStockMovement(CinemaServiceAgent.RecordStockMovementRequest.fromJS({
      foodAndDrinkId: this._data.item.id,
      type: v.type,
      quantity: Number(v.quantity),
      reasonCode: v.reasonCode,
      note: note || null,
    })).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({ message: this._translate.instant('inventory.toast.movementSaved') }));
        this._dialogRef.close(true);
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }

  cancel(): void {
    this._dialogRef.close(false);
  }
}

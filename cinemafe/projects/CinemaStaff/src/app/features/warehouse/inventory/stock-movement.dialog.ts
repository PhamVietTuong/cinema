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
  templateUrl: './stock-movement.dialog.html',
})
export class StockMovementDialog {
  readonly types = [
    { value: CinemaServiceAgent.StockMovementType.Adjust, name: 'warehouse.movementType.adjust' },
    { value: CinemaServiceAgent.StockMovementType.Waste, name: 'warehouse.movementType.waste' },
  ];
  /** OpeningBalance and StockCountCorrection are system reasons (the count dialog writes the latter). */
  readonly reasons = StockReasonCodeValues.filter(r => r.value !== CinemaServiceAgent.StockReasonCode.OpeningBalance
    && r.value !== CinemaServiceAgent.StockReasonCode.StockCountCorrection);
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

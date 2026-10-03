import { Component, Inject } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { CinemaServiceAgent, SharedModule } from 'CinemaLib';

export interface ReceivePlanDialogData {
  items: CinemaServiceAgent.StoragePlanItemDTO[];
}

/** Lists plan items with an editable actual quantity (defaults to planned). Resolves the items to send, or undefined on cancel. */
@Component({
  selector: 'app-receive-plan-dialog',
  standalone: true,
  imports: [SharedModule],
  template: `
<div mat-dialog-title class="dialog-title">{{ 'storagePlans.receive.title' | translate }}</div>

<form [formGroup]="form" (ngSubmit)="submit()">
  <mat-dialog-content>
    <p class="hint">{{ 'storagePlans.receive.hint' | translate }}</p>
    <div formArrayName="rows" class="recv-list">
      @for (item of items; track item.id; let i = $index) {
        <div class="recv-row" [formGroupName]="i">
          <div class="recv-name">
            <strong>{{ item.foodAndDrinkName }}</strong>
            <span class="hint">{{ 'storagePlans.receive.planned' | translate }}: {{ item.plannedQuantity }}</span>
          </div>
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="recv-qty">
            <mat-label>{{ 'storagePlans.receive.actual' | translate }}</mat-label>
            <input matInput type="number" min="0" max="100000" step="1" formControlName="quantity">
            @if (rowGroup(i).controls['quantity'].invalid) {
              <mat-error>{{ 'storagePlans.errors.receivedRange' | translate }}</mat-error>
            }
          </mat-form-field>
        </div>
      }
    </div>
  </mat-dialog-content>

  <div mat-dialog-actions class="dialog-actions">
    <button mat-raised-button type="button" (click)="cancel()">{{ 'common.cancel' | translate }}</button>
    <button mat-raised-button color="primary" type="submit">{{ 'storagePlans.actions.receive' | translate }}</button>
  </div>
</form>

<button mat-icon-button type="button" class="dialog-close-btn" (click)="cancel()">
  <mat-icon>close</mat-icon>
</button>
`,
  styles: [`
    .hint { color: var(--ml-muted, #777); font-size: 13px; }
    .recv-list { display: flex; flex-direction: column; gap: 12px; margin-top: 8px; }
    .recv-row { display: flex; align-items: center; justify-content: space-between; gap: 12px; flex-wrap: wrap; }
    .recv-name { display: flex; flex-direction: column; min-width: 140px; flex: 1 1 160px; }
    .recv-qty { width: 140px; }
  `],
})
export class ReceivePlanDialog {
  readonly items: CinemaServiceAgent.StoragePlanItemDTO[];
  form: FormGroup;

  constructor(
    private _fb: FormBuilder,
    private _dialogRef: MatDialogRef<ReceivePlanDialog, CinemaServiceAgent.ReceiveStoragePlanItem[]>,
    @Inject(MAT_DIALOG_DATA) data: ReceivePlanDialogData,
  ) {
    this.items = data.items;
    this.form = this._fb.group({
      rows: this._fb.array(this.items.map(item => this._fb.group({
        quantity: [item.plannedQuantity ?? 0, [Validators.required, Validators.min(0), Validators.max(100000), Validators.pattern(/^\d+$/)]],
      }))),
    });
  }

  rowGroup(index: number): FormGroup {
    return (this.form.controls['rows'] as FormArray).at(index) as FormGroup;
  }

  submit(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const rows = (this.form.controls['rows'] as FormArray).value as { quantity: number }[];
    this._dialogRef.close(this.items.map((item, i) => CinemaServiceAgent.ReceiveStoragePlanItem.fromJS({
      storagePlanItemId: item.id,
      receivedQuantity: Number(rows[i].quantity),
    })));
  }

  cancel(): void {
    this._dialogRef.close(undefined);
  }
}

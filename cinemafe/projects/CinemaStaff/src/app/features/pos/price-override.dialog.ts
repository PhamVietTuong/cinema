import { Component, Inject } from '@angular/core';
import { FormControl, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { SharedModule } from 'CinemaLib';

export interface PriceOverrideDialogData {
  /** Name of the line being repriced. */
  label: string;
  /** The price in force now (list or already overridden). */
  current: number;
  /** True when the line already carries an override, so it can be reset. */
  hasOverride: boolean;
}

/** Asks for a replacement price. Resolves the new price, `null` to restore the list price, or undefined on cancel. */
@Component({
  selector: 'staff-price-override-dialog',
  standalone: true,
  imports: [SharedModule],
  template: `
    <div mat-dialog-title class="dialog-title">{{ 'pos.override.title' | translate }}</div>
    <mat-dialog-content>
      <p>{{ data.label }}</p>
      <mat-form-field appearance="outline" subscriptSizing="dynamic" style="width: 100%">
        <mat-label>{{ 'pos.override.newPrice' | translate }}</mat-label>
        <input matInput type="number" min="0" step="1000" [formControl]="price" (keydown.enter)="apply()">
        @if (price.invalid) {
          <mat-error>{{ 'pos.override.invalid' | translate }}</mat-error>
        }
      </mat-form-field>
      <p class="hint">{{ 'pos.override.hint' | translate }}</p>
    </mat-dialog-content>
    <div mat-dialog-actions class="dialog-actions">
      <button mat-raised-button type="button" (click)="ref.close(undefined)">{{ 'common.cancel' | translate }}</button>
      @if (data.hasOverride) {
        <button mat-raised-button type="button" (click)="ref.close(null)">{{ 'pos.override.reset' | translate }}</button>
      }
      <button mat-raised-button color="primary" type="button" (click)="apply()">{{ 'pos.override.apply' | translate }}</button>
    </div>
  `,
  styles: [`.hint { color: var(--ml-muted); font-size: 13px; }`],
})
export class PriceOverrideDialog {
  readonly price: FormControl<number | null>;

  constructor(
    public ref: MatDialogRef<PriceOverrideDialog, number | null | undefined>,
    @Inject(MAT_DIALOG_DATA) public data: PriceOverrideDialogData,
  ) {
    this.price = new FormControl<number | null>(data.current, [Validators.required, Validators.min(0)]);
  }

  apply(): void {
    if (this.price.invalid || this.price.value === null) {
      this.price.markAsTouched();
      return;
    }
    this.ref.close(Math.round(this.price.value));
  }
}

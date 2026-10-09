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
  templateUrl: './price-override.dialog.html',
  styleUrl: './price-override.dialog.scss',
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

import { Component, Inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { TranslatePipe } from '@ngx-translate/core';

export interface DiscountCodeDialogData {
  code: string;
}

/** Small prompt for the checkout page's discount code. Closes with the trimmed code ('' clears it) or `undefined` on cancel. */
@Component({
  selector: 'app-discount-code-dialog',
  standalone: true,
  imports: [FormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, TranslatePipe],
  template: `
    <h2 mat-dialog-title>{{ 'booking.summary.discountDialogTitle' | translate }}</h2>
    <mat-dialog-content>
      <mat-form-field appearance="outline" class="code-field">
        <mat-label>{{ 'booking.summary.discountCode' | translate }}</mat-label>
        <input matInput cdkFocusInitial [(ngModel)]="code" (keydown.enter)="apply()"
               [placeholder]="'booking.summary.discountPlaceholder' | translate">
      </mat-form-field>
      <p class="note">{{ 'booking.summary.discountNote' | translate }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="cancel()">{{ 'common.cancel' | translate }}</button>
      <button mat-flat-button type="button" (click)="apply()">{{ 'booking.summary.discountApply' | translate }}</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .code-field { width: 100%; min-width: 280px; }
    .note { margin: 0; font-size: 0.8rem; opacity: 0.7; }
  `],
})
export class DiscountCodeDialog {
  code: string;

  constructor(
    private _dialogRef: MatDialogRef<DiscountCodeDialog, string>,
    @Inject(MAT_DIALOG_DATA) data: DiscountCodeDialogData,
  ) {
    this.code = data.code;
  }

  apply(): void {
    this._dialogRef.close(this.code.trim());
  }

  cancel(): void {
    this._dialogRef.close();
  }
}

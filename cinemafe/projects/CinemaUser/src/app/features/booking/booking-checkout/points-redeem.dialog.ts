import { Component, Inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@ngx-translate/core';

export interface PointsRedeemDialogData {
  balance: number;
  /** Most points redeemable on this order (balance capped by the order total). */
  max: number;
  value: number;
  /** VND worth of one point. */
  pointValue: number;
}

/** Picks how many reward points to redeem. Closes with the chosen amount, or `undefined` on cancel. */
@Component({
  selector: 'app-points-redeem-dialog',
  standalone: true,
  imports: [DecimalPipe, FormsModule, MatDialogModule, MatButtonModule, MatIconModule, TranslatePipe],
  template: `
    <h2 mat-dialog-title>{{ 'booking.summary.redeemPoints' | translate }}</h2>
    <mat-dialog-content>
      <div class="balance">
        <span class="balance-num">{{ data.balance | number:'1.0-0' }}</span>
        <span class="balance-label">{{ 'booking.summary.pointsAvailableLabel' | translate }}</span>
      </div>

      <div class="stepper">
        <button type="button" mat-stroked-button class="step-btn" (click)="set(points - 1)" [disabled]="points <= 0"
                [attr.aria-label]="'booking.summary.pointsDecrease' | translate">−</button>
        <input class="points-input" type="number" min="0" step="1" [max]="data.max" [disabled]="data.max === 0"
               [ngModel]="points" (ngModelChange)="set($event)" [attr.aria-label]="'booking.summary.pointsPlaceholder' | translate">
        <button type="button" mat-stroked-button class="step-btn" (click)="set(points + 1)" [disabled]="points >= data.max"
                [attr.aria-label]="'booking.summary.pointsIncrease' | translate">+</button>
        <button type="button" mat-button (click)="set(data.max)" [disabled]="data.max === 0 || points === data.max">
          {{ 'booking.summary.pointsUseMax' | translate }}
        </button>
      </div>

      <p class="saving">{{ 'booking.summary.pointsSaving' | translate:{ amount: (points * data.pointValue | number:'1.0-0') } }}</p>
      <p class="note">{{ 'booking.summary.pointsHint' | translate:{ max: data.max } }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="cancel()">{{ 'common.cancel' | translate }}</button>
      <button mat-flat-button type="button" (click)="apply()">{{ 'booking.summary.discountApply' | translate }}</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .balance { display: flex; align-items: baseline; gap: 8px; margin-bottom: 16px; }
    .balance-num { font-size: 2rem; font-weight: 600; font-variant-numeric: tabular-nums; }
    .balance-label { opacity: 0.7; }
    .stepper { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; min-width: 280px; }
    .step-btn { min-width: 40px; padding: 0; font-size: 1.2rem; }
    .points-input {
      width: 90px; height: 40px; text-align: center; font-size: 1.1rem; font-variant-numeric: tabular-nums;
      border: 1px solid currentColor; border-radius: 6px; background: transparent; color: inherit;
    }
    .saving { margin: 16px 0 4px; font-weight: 600; }
    .note { margin: 0; font-size: 0.8rem; opacity: 0.7; }
  `],
})
export class PointsRedeemDialog {
  points: number;

  constructor(
    private _dialogRef: MatDialogRef<PointsRedeemDialog, number>,
    @Inject(MAT_DIALOG_DATA) public data: PointsRedeemDialogData,
  ) {
    this.points = data.value;
  }

  set(value: number): void {
    const n = Math.floor(Number(value) || 0);
    this.points = Math.max(0, Math.min(n, this.data.max));
  }

  apply(): void {
    this._dialogRef.close(this.points);
  }

  cancel(): void {
    this._dialogRef.close();
  }
}

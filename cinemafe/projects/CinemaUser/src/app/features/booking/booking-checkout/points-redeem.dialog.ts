import { Component, Inject, OnDestroy } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
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
  imports: [DecimalPipe, MatDialogModule, MatIconModule, TranslatePipe],
  templateUrl: './points-redeem.dialog.html',
  styleUrls: ['./ticket-dialog.scss', './points-redeem.dialog.scss'],
})
export class PointsRedeemDialog implements OnDestroy {
  points: number;

  private _holdDelay: ReturnType<typeof setTimeout> | null = null;
  private _holdTick: ReturnType<typeof setInterval> | null = null;

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

  nudge(direction: number): void {
    this.set(this.points + direction);
  }

  /**
   * Press steps once straight away; holding past a short delay keeps counting toward the limit,
   * speeding up the longer the button stays down (1, then 2, then 5 points per tick) and stopping
   * on its own at 0 or the maximum.
   */
  startHold(direction: number, event: PointerEvent): void {
    event.preventDefault();
    this.stopHold();
    this.nudge(direction);
    this._holdDelay = setTimeout(() => {
      let ticks = 0;
      this._holdTick = setInterval(() => {
        ticks++;
        const step = ticks > 25 ? 5 : ticks > 10 ? 2 : 1;
        this.set(this.points + direction * step);
        if (this.points === 0 || this.points === this.data.max) {
          this.stopHold();
        }
      }, 80);
    }, 350);
  }

  stopHold(): void {
    if (this._holdDelay) {
      clearTimeout(this._holdDelay);
      this._holdDelay = null;
    }
    if (this._holdTick) {
      clearInterval(this._holdTick);
      this._holdTick = null;
    }
  }

  apply(): void {
    this._dialogRef.close(this.points);
  }

  cancel(): void {
    this._dialogRef.close();
  }

  ngOnDestroy(): void {
    this.stopHold();
  }
}

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
  template: `
    <div class="tk-top">
      <span class="tk-badge"><mat-icon>star</mat-icon></span>
      <h2 mat-dialog-title class="tk-title">{{ 'booking.summary.redeemPoints' | translate }}</h2>
    </div>
    <div class="tk-body">
      <p class="tk-note centered">{{ 'booking.summary.pointsBalance' | translate:{ points: data.balance } }}</p>

      <div class="stepper">
        <button type="button" class="round" [disabled]="points <= 0"
                [attr.aria-label]="'booking.summary.pointsDecrease' | translate"
                (pointerdown)="startHold(-1, $event)" (pointerup)="stopHold()" (pointerleave)="stopHold()" (pointercancel)="stopHold()"
                (keydown.enter)="nudge(-1)" (keydown.space)="nudge(-1); $event.preventDefault()">−</button>
        <span class="big" aria-live="polite">{{ points }}</span>
        <button type="button" class="round" [disabled]="points >= data.max"
                [attr.aria-label]="'booking.summary.pointsIncrease' | translate"
                (pointerdown)="startHold(1, $event)" (pointerup)="stopHold()" (pointerleave)="stopHold()" (pointercancel)="stopHold()"
                (keydown.enter)="nudge(1)" (keydown.space)="nudge(1); $event.preventDefault()">+</button>
      </div>

      <p class="saving">{{ 'booking.summary.pointsSaving' | translate:{ amount: (points * data.pointValue | number:'1.0-0') } }}</p>
      <p class="tk-note centered">{{ 'booking.summary.pointsHint' | translate:{ max: data.max } }}</p>
    </div>
    <div class="tk-foot">
      <button type="button" class="tk-btn full" (click)="apply()">{{ 'booking.summary.discountApply' | translate }}</button>
      <button type="button" class="tk-btn ghost" (click)="set(data.max)" [disabled]="data.max === 0 || points === data.max">
        {{ 'booking.summary.pointsUseMax' | translate }}
      </button>
      <button type="button" class="tk-btn text" (click)="cancel()">{{ 'common.cancel' | translate }}</button>
    </div>
  `,
  styleUrl: './ticket-dialog.scss',
  styles: [`
    .centered { text-align: center; }
    .stepper { display: flex; align-items: center; justify-content: center; gap: 18px; }
    .round {
      width: 48px; height: 48px; border-radius: 50%; cursor: pointer; font-size: 1.5rem; line-height: 1;
      border: 1px solid var(--ml-rule-strong); background: var(--ml-panel); color: var(--ml-ink);
      user-select: none; touch-action: manipulation;
      &:hover:not(:disabled) { border-color: var(--ml-action); }
      &:disabled { opacity: 0.4; cursor: not-allowed; }
      &:focus-visible { outline: var(--cx-focus-width) solid var(--ml-action); outline-offset: var(--cx-focus-offset); }
    }
    .big {
      min-width: 3ch; text-align: center;
      font-family: var(--ml-font-data); font-size: 2.4rem; font-weight: 500; line-height: 1;
      font-variant-numeric: tabular-nums; color: var(--ml-ink);
    }
    .saving { margin: 0; text-align: center; font-weight: 600; color: var(--ml-success-ink); }
  `],
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

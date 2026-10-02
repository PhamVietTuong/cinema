import { Component, DestroyRef, Inject, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Observable, Subject, debounceTime, distinctUntilChanged, switchMap, tap } from 'rxjs';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslatePipe } from '@ngx-translate/core';

export interface DiscountCheckResult {
  valid: boolean;
  message: string;
  /** What the code would take off the order, in VND (0 when invalid). */
  discountAmount: number;
}

export interface DiscountCodeDialogData {
  code: string;
  /** Asks the server whether a code can be used for this order and what it is worth. */
  check: (code: string) => Observable<DiscountCheckResult>;
}

export interface DiscountCodeDialogResult {
  code: string;
  valid: boolean | null;
  message: string;
  discountAmount: number;
}

/**
 * Prompt for the checkout page's discount code. The code is checked as it is typed (after a short
 * pause) and on Enter, so Apply only enables for a code the server accepts. Closes with the verified
 * code, an empty code to remove it, or `undefined` on cancel.
 */
@Component({
  selector: 'app-discount-code-dialog',
  standalone: true,
  imports: [FormsModule, MatDialogModule, MatIconModule, MatProgressSpinnerModule, TranslatePipe],
  template: `
    <div class="tk-top">
      <span class="tk-badge"><mat-icon>local_offer</mat-icon></span>
      <h2 mat-dialog-title class="tk-title">{{ 'booking.summary.discountDialogTitle' | translate }}</h2>
    </div>
    <div class="tk-body">
      <div class="input-wrap">
        <input class="tk-input" cdkFocusInitial [(ngModel)]="code" (ngModelChange)="onCodeChange($event)" (keydown.enter)="checkNow()"
               [attr.aria-label]="'booking.summary.discountCode' | translate"
               [placeholder]="'booking.summary.discountPlaceholder' | translate">
        @if (checking) {
          <mat-spinner class="spin" diameter="18"></mat-spinner>
        }
      </div>
      @if (message) {
        <p class="tk-result" [class.ok]="valid" [class.bad]="valid === false" aria-live="polite">{{ message }}</p>
      } @else {
        <p class="tk-note">{{ 'booking.summary.discountNote' | translate }}</p>
      }
    </div>
    <div class="tk-foot">
      <button type="button" class="tk-btn full" (click)="apply()" [disabled]="!valid">{{ 'booking.summary.discountApply' | translate }}</button>
      @if (data.code) {
        <button type="button" class="tk-btn text" (click)="remove()">{{ 'booking.summary.giftCardRemove' | translate }}</button>
      }
      <button type="button" class="tk-btn text" (click)="cancel()">{{ 'common.cancel' | translate }}</button>
    </div>
  `,
  styleUrl: './ticket-dialog.scss',
  styles: [`
    .input-wrap { position: relative; }
    .spin { position: absolute; right: 12px; top: 14px; }
  `],
})
export class DiscountCodeDialog implements OnInit {
  /** How long typing must pause before the code is sent to the server. */
  static readonly CHECK_DELAY_MS = 500;

  code: string;
  valid: boolean | null = null;
  message = '';
  discountAmount = 0;
  checking = false;

  private _typed$ = new Subject<string>();

  constructor(
    private _dialogRef: MatDialogRef<DiscountCodeDialog, DiscountCodeDialogResult>,
    @Inject(MAT_DIALOG_DATA) public data: DiscountCodeDialogData,
    destroyRef: DestroyRef,
  ) {
    this.code = data.code;
    // switchMap drops the answer for an older code once a newer one is typed, so a slow reply can't
    // overwrite the verdict for what is on screen now.
    this._typed$.pipe(
      debounceTime(DiscountCodeDialog.CHECK_DELAY_MS),
      distinctUntilChanged(),
      tap(() => { this.checking = true; }),
      switchMap(code => this.data.check(code)),
      takeUntilDestroyed(destroyRef),
    ).subscribe(result => this._show(result));
  }

  ngOnInit(): void {
    // Re-open with a code already entered: confirm it straight away.
    if (this.code.trim()) {
      this.checkNow();
    }
  }

  /** Any edit discards the earlier verdict so Apply can never submit an unchecked code. */
  onCodeChange(value: string): void {
    this.valid = null;
    this.message = '';
    this.discountAmount = 0;
    this.checking = false;
    const code = value.trim();
    if (code) {
      this._typed$.next(code);
    }
  }

  checkNow(): void {
    const code = this.code.trim();
    if (!code) {
      return;
    }
    this.checking = true;
    this.data.check(code).subscribe(result => this._show(result));
  }

  apply(): void {
    if (this.valid) {
      this._dialogRef.close({ code: this.code.trim(), valid: true, message: this.message, discountAmount: this.discountAmount });
    }
  }

  remove(): void {
    this._dialogRef.close({ code: '', valid: null, message: '', discountAmount: 0 });
  }

  cancel(): void {
    this._dialogRef.close();
  }

  private _show(result: DiscountCheckResult): void {
    this.valid = result.valid;
    this.message = result.message;
    this.discountAmount = result.discountAmount;
    this.checking = false;
  }
}

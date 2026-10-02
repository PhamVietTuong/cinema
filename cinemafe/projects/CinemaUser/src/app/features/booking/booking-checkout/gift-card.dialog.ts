import { Component, Inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslatePipe } from '@ngx-translate/core';

export interface GiftCardCheckResult {
  valid: boolean;
  message: string;
}

export interface GiftCardDialogData {
  code: string;
  /** Asks the server whether a code is usable and what it is worth. */
  check: (code: string) => Observable<GiftCardCheckResult>;
}

export interface GiftCardDialogResult {
  code: string;
  valid: boolean | null;
  message: string;
}

/** Checks a gift card code. Closes with the verified card, an empty code to remove it, or `undefined` on cancel. */
@Component({
  selector: 'app-gift-card-dialog',
  standalone: true,
  imports: [FormsModule, MatDialogModule, MatIconModule, MatProgressSpinnerModule, TranslatePipe],
  template: `
    <div class="tk-top">
      <span class="tk-badge"><mat-icon>card_giftcard</mat-icon></span>
      <h2 mat-dialog-title class="tk-title">{{ 'booking.summary.giftCardDialogTitle' | translate }}</h2>
    </div>
    <div class="tk-body">
      <input class="tk-input" cdkFocusInitial [(ngModel)]="code" (ngModelChange)="onCodeChange()" (keydown.enter)="check()"
             [attr.aria-label]="'booking.summary.giftCardCode' | translate"
             [placeholder]="'booking.summary.giftCardPlaceholder' | translate">
      <button type="button" class="tk-btn ghost" (click)="check()" [disabled]="checking || !code.trim()">
        @if (checking) { <mat-spinner diameter="18"></mat-spinner> } @else { {{ 'booking.summary.giftCardApply' | translate }} }
      </button>
      @if (message) {
        <p class="tk-result" [class.ok]="valid" [class.bad]="valid === false">{{ message }}</p>
      } @else {
        <p class="tk-note">{{ 'booking.summary.giftCardNote' | translate }}</p>
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
})
export class GiftCardDialog {
  code: string;
  valid: boolean | null = null;
  message = '';
  checking = false;

  constructor(
    private _dialogRef: MatDialogRef<GiftCardDialog, GiftCardDialogResult>,
    @Inject(MAT_DIALOG_DATA) public data: GiftCardDialogData,
  ) {
    this.code = data.code;
  }

  /** Editing the code invalidates any earlier verdict, so Apply can't submit an unchecked code. */
  onCodeChange(): void {
    this.valid = null;
    this.message = '';
  }

  check(): void {
    const code = this.code.trim();
    if (!code || this.checking) {
      return;
    }
    this.checking = true;
    this.data.check(code).subscribe(result => {
      this.valid = result.valid;
      this.message = result.message;
      this.checking = false;
    });
  }

  apply(): void {
    if (this.valid) {
      this._dialogRef.close({ code: this.code.trim(), valid: true, message: this.message });
    }
  }

  remove(): void {
    this._dialogRef.close({ code: '', valid: null, message: '' });
  }

  cancel(): void {
    this._dialogRef.close();
  }
}

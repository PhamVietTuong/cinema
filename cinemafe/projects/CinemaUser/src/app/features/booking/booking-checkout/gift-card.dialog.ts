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
  templateUrl: './gift-card.dialog.html',
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

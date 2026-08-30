import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@ngx-translate/core';

export interface ConfirmDialogData {
  /** i18n key for the message body — translated inside the dialog, not by the caller. */
  message: string;
  /** i18n key; defaults to 'common.confirm'. */
  title?: string;
  /** i18n key; defaults to 'common.confirm'. */
  confirmText?: string;
  /** i18n key; defaults to 'common.cancel'. */
  cancelText?: string;
}

/** Generic Yes/No confirmation, opened via DialogService.openConfirmDialog(). Resolves `true` on confirm, `false` on cancel/close. */
@Component({
  selector: 'cl-confirm-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule, TranslatePipe],
  templateUrl: './confirm.dialog.html',
})
export class ConfirmDialog {
  constructor(
    private _dialogRef: MatDialogRef<ConfirmDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) public data: ConfirmDialogData,
  ) {}

  confirm(): void {
    this._dialogRef.close(true);
  }

  cancel(): void {
    this._dialogRef.close(false);
  }
}

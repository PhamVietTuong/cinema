import { Component, Inject } from '@angular/core';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@ngx-translate/core';

export interface IdCheckNoticeDialogData {
  categoryName: string;
  /** Admin-provided override for the notice body; null/empty falls back to the default translated text. */
  customMessage?: string | null;
}

/**
 * Mandatory acknowledgment shown the first time a customer adds a ticket of a category flagged
 * `requiresIdCheck` (student/senior/under-22 style categories). Resolves `true` on confirm,
 * `undefined` on cancel/Esc/backdrop — the caller only commits the ticket-count change on `true`.
 */
@Component({
  selector: 'app-id-check-notice-dialog',
  standalone: true,
  imports: [MatDialogModule, MatIconModule, TranslatePipe],
  templateUrl: './id-check-notice.dialog.html',
  styleUrls: ['../booking-checkout/ticket-dialog.scss', './id-check-notice.dialog.scss'],
})
export class IdCheckNoticeDialog {
  constructor(
    private _dialogRef: MatDialogRef<IdCheckNoticeDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) public data: IdCheckNoticeDialogData,
  ) {}

  confirm(): void {
    this._dialogRef.close(true);
  }

  cancel(): void {
    this._dialogRef.close();
  }
}

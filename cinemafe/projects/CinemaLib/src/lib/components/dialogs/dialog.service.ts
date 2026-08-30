import { Injectable } from '@angular/core';
import { MatDialog, MatDialogConfig, MatDialogRef } from '@angular/material/dialog';
import { ConfirmDialog, ConfirmDialogData } from './confirm.dialog';

/** Drives the panel's accent colour for a given dialog (e.g. warn-tinted for a destructive confirm). */
export enum SeverityEnum {
  INFO = 'Info',
  WARN = 'Warn',
  ERROR = 'Error',
}

/** Opens the library's shared, concise dialogs (confirm, ...) so pages don't each roll their own. */
@Injectable({ providedIn: 'root' })
export class DialogService {
  constructor(private _matDialog: MatDialog) {}

  openConfirmDialog(data: ConfirmDialogData, config?: MatDialogConfig): MatDialogRef<ConfirmDialog, boolean> {
    return this._matDialog.open(ConfirmDialog, { width: '400px', panelClass: SeverityEnum.WARN, ...config, data });
  }
}

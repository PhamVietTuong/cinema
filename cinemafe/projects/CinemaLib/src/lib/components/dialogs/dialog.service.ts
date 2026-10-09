import { Injectable } from '@angular/core';
import { MatDialog, MatDialogConfig, MatDialogRef } from '@angular/material/dialog';
import { ConfirmDialog, ConfirmDialogData } from './confirm.dialog';
import { ReasonDialogComponent, ReasonDialogData, ReasonDialogResult } from './reason.dialog';
import { ManagerOverrideDialogComponent, ManagerOverrideDialogData } from './manager-override.dialog';
import { StaffServiceAgent } from '../../services/staff-http.service';

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

  /** Confirm/reject/receive-with-reason dialog; resolves a ReasonDialogResult, or undefined on cancel. */
  openReasonDialog(data: ReasonDialogData, config?: MatDialogConfig): MatDialogRef<ReasonDialogComponent, ReasonDialogResult | undefined> {
    return this._matDialog.open(ReasonDialogComponent, { width: '520px', maxWidth: '95vw', ...config, data });
  }

  /**
   * Manager PIN approval for a sensitive staff action; resolves the ManagerOverrideDTO to send with the request, or undefined on
   * cancel. Do not open it for a caller who is already an approver (the API needs no PIN for them).
   */
  openManagerOverrideDialog(data: ManagerOverrideDialogData, config?: MatDialogConfig): MatDialogRef<ManagerOverrideDialogComponent, StaffServiceAgent.ManagerOverrideDTO | undefined> {
    return this._matDialog.open(ManagerOverrideDialogComponent, { width: '420px', maxWidth: '95vw', ...config, data });
  }
}

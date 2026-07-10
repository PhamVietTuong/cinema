import { inject, Injectable } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

/** Thin wrapper over MatSnackBar for consistent app-wide toasts. */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private _sb = inject(MatSnackBar);

  success(message: string): void { this._show(message, 'toast-success'); }
  error(message: string): void { this._show(message, 'toast-error'); }
  info(message: string): void { this._show(message, 'toast-info'); }

  private _show(message: string, panelClass: string): void {
    this._sb.open(message, 'Đóng', {
      duration: 4000,
      horizontalPosition: 'right',
      verticalPosition: 'top',
      panelClass: [panelClass],
    });
  }
}

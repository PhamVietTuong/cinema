import { Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef } from '@angular/material/dialog';
import { SharedModule } from 'CinemaLib';

/** Asks for the mandatory rejection reason. Resolves the trimmed reason, or undefined on cancel. */
@Component({
  selector: 'app-reject-plan-dialog',
  standalone: true,
  imports: [SharedModule],
  template: `
<div mat-dialog-title class="dialog-title">{{ 'storagePlans.reject.title' | translate }}</div>

<form [formGroup]="form" (ngSubmit)="submit()">
  <mat-dialog-content>
    <div class="dlg-grid">
      <div class="dlg-row">
        <div class="dlg-field">
          <label class="dlg-label" for="rj-reason">{{ 'storagePlans.reject.reason' | translate }}</label>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <textarea matInput id="rj-reason" rows="4" maxlength="500" formControlName="reason"
              [placeholder]="'storagePlans.reject.reasonPlaceholder' | translate"></textarea>
            @if (form.controls['reason'].hasError('required')) {
              <mat-error>{{ 'common.required' | translate }}</mat-error>
            }
          </mat-form-field>
        </div>
      </div>
    </div>
  </mat-dialog-content>

  <div mat-dialog-actions class="dialog-actions">
    <button mat-raised-button type="button" (click)="cancel()">{{ 'common.cancel' | translate }}</button>
    <button mat-raised-button color="warn" type="submit">{{ 'storagePlans.actions.reject' | translate }}</button>
  </div>
</form>

<button mat-icon-button type="button" class="dialog-close-btn" (click)="cancel()">
  <mat-icon>close</mat-icon>
</button>
`,
})
export class RejectPlanDialog {
  form: FormGroup;

  constructor(
    private _fb: FormBuilder,
    private _dialogRef: MatDialogRef<RejectPlanDialog, string>,
  ) {
    this.form = this._fb.group({
      reason: ['', [Validators.required, Validators.pattern(/\S/)]],
    });
  }

  submit(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    this._dialogRef.close((this.form.value.reason as string).trim());
  }

  cancel(): void {
    this._dialogRef.close(undefined);
  }
}

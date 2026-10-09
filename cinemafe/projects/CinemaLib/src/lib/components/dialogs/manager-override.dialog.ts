import { ChangeDetectorRef, Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TranslatePipe } from '@ngx-translate/core';
import { StaffServiceAgent } from '../../services/staff-http.service';

export interface ManagerOverrideDialogData {
  /** Theater whose on-duty approvers may confirm (admins must pass one). */
  theaterId?: string;
  /** i18n key of the dialog title (default `override.title`). */
  titleKey?: string;
  /** i18n key of the explanation shown above the fields (default `override.hint`). */
  hintKey?: string;
}

/**
 * In-place manager approval: pick an approver on duty and type their PIN. Resolves the `ManagerOverrideDTO` to send
 * with the sensitive request, or undefined on cancel. A caller who is already an approver must NOT open this dialog
 * (the API needs no PIN for them). The PIN is never cached: the form is destroyed with the dialog.
 * Opened through `DialogService.openManagerOverrideDialog()`; the app must provide `StaffServiceAgent.WorkforceHttpService`.
 */
@Component({
  selector: 'cl-manager-override-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule,
    MatIconModule, MatInputModule, MatSelectModule, TranslatePipe,
  ],
  template: `
<div mat-dialog-title class="dialog-title">{{ (data.titleKey ?? 'override.title') | translate }}</div>

<form [formGroup]="form" (ngSubmit)="submit()" autocomplete="off">
  <mat-dialog-content>
    <p class="cl-override-hint">{{ (data.hintKey ?? 'override.hint') | translate }}</p>
    <div class="cl-override-fields">
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>{{ 'override.approver' | translate }}</mat-label>
        <mat-select formControlName="approverUserId">
          @for (a of approvers; track a.id) {
            <mat-option [value]="a.id">{{ a.name }}</mat-option>
          }
        </mat-select>
        @if (loaded && approvers.length === 0) {
          <mat-hint>{{ 'override.noApprovers' | translate }}</mat-hint>
        }
        @if (form.controls['approverUserId'].hasError('required')) {
          <mat-error>{{ 'common.required' | translate }}</mat-error>
        }
      </mat-form-field>

      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>{{ 'override.pin' | translate }}</mat-label>
        <input matInput type="password" inputmode="numeric" autocomplete="new-password" maxlength="8" formControlName="pin">
        @if (form.controls['pin'].invalid && form.controls['pin'].touched) {
          <mat-error>{{ 'override.pinInvalid' | translate }}</mat-error>
        }
      </mat-form-field>
    </div>
  </mat-dialog-content>

  <div mat-dialog-actions class="dialog-actions">
    <button mat-raised-button type="button" (click)="cancel()">{{ 'common.cancel' | translate }}</button>
    <button mat-raised-button color="primary" type="submit">{{ 'override.confirm' | translate }}</button>
  </div>
</form>

<button mat-icon-button type="button" class="dialog-close-btn" (click)="cancel()">
  <mat-icon>close</mat-icon>
</button>
`,
  styles: [`
    .cl-override-hint { color: var(--ml-muted, #777); font-size: 13px; }
    .cl-override-fields { display: flex; flex-direction: column; gap: 16px; margin-top: 8px; }
  `],
})
export class ManagerOverrideDialogComponent implements OnInit {
  form: FormGroup;
  approvers: StaffServiceAgent.OverrideApproverDTO[] = [];
  loaded = false;

  constructor(
    fb: FormBuilder,
    private _workforce: StaffServiceAgent.WorkforceHttpService,
    private _cdr: ChangeDetectorRef,
    private _dialogRef: MatDialogRef<ManagerOverrideDialogComponent, StaffServiceAgent.ManagerOverrideDTO | undefined>,
    @Inject(MAT_DIALOG_DATA) public data: ManagerOverrideDialogData,
  ) {
    this.form = fb.group({
      approverUserId: [null, Validators.required],
      pin: ['', [Validators.required, Validators.pattern(/^\d{4,8}$/)]],
    });
  }

  ngOnInit(): void {
    this._workforce.getOverrideApprovers(StaffServiceAgent.OverrideApproversRequest.fromJS({ theaterId: this.data.theaterId })).subscribe({
      next: approvers => {
        this.approvers = approvers ?? [];
        if (this.approvers.length === 1) {
          this.form.controls['approverUserId'].setValue(this.approvers[0].id);
        }
        this.loaded = true;
        this._cdr.markForCheck();
      },
      error: () => {
        this.loaded = true;
        this._cdr.markForCheck();
      },
    });
  }

  submit(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    this._dialogRef.close(StaffServiceAgent.ManagerOverrideDTO.fromJS({
      approverUserId: this.form.value.approverUserId,
      pin: this.form.value.pin,
    }));
  }

  cancel(): void {
    this._dialogRef.close(undefined);
  }
}

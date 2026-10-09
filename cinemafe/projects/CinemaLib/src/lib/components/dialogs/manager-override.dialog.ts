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
import { CinemaServiceAgent } from '../../services/cinema-http.service';

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
 * Opened through `DialogService.openManagerOverrideDialog()`; the app must provide `CinemaServiceAgent.HttpService`.
 */
@Component({
  selector: 'cl-manager-override-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule,
    MatIconModule, MatInputModule, MatSelectModule, TranslatePipe,
  ],
  templateUrl: './manager-override.dialog.html',
  styleUrl: './manager-override.dialog.scss',
})
export class ManagerOverrideDialogComponent implements OnInit {
  form: FormGroup;
  approvers: CinemaServiceAgent.OverrideApproverDTO[] = [];
  loaded = false;

  constructor(
    fb: FormBuilder,
    private _workforce: CinemaServiceAgent.HttpService,
    private _cdr: ChangeDetectorRef,
    private _dialogRef: MatDialogRef<ManagerOverrideDialogComponent, CinemaServiceAgent.ManagerOverrideDTO | undefined>,
    @Inject(MAT_DIALOG_DATA) public data: ManagerOverrideDialogData,
  ) {
    this.form = fb.group({
      approverUserId: [null, Validators.required],
      pin: ['', [Validators.required, Validators.pattern(/^\d{4,8}$/)]],
    });
  }

  ngOnInit(): void {
    this._workforce.getOverrideApprovers(CinemaServiceAgent.OverrideApproversRequest.fromJS({ theaterId: this.data.theaterId })).subscribe({
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
    this._dialogRef.close(CinemaServiceAgent.ManagerOverrideDTO.fromJS({
      approverUserId: this.form.value.approverUserId,
      pin: this.form.value.pin,
    }));
  }

  cancel(): void {
    this._dialogRef.close(undefined);
  }
}

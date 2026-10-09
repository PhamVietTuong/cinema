import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { TranslatePipe } from '@ngx-translate/core';

/** A reason code the user may pick. */
export interface ReasonDialogCode {
  value: string | number;
  /** i18n key. */
  labelKey: string;
}

/** One editable quantity row (e.g. a received line). */
export interface ReasonDialogLine {
  id: string;
  /** Already-translated row title. */
  label: string;
  /** Already-translated secondary text under the title. */
  hint?: string;
  /** Initial quantity. */
  value: number;
}

export interface ReasonDialogData {
  /** i18n key of the dialog title. */
  titleKey: string;
  /** i18n key of an explanatory paragraph above the fields. */
  hintKey?: string;
  /** i18n key of the submit button. */
  confirmKey: string;
  /** Submit button colour (default `primary`). */
  confirmColor?: 'primary' | 'warn';
  /** Offer a required reason-code select. */
  codes?: { labelKey: string; options: readonly ReasonDialogCode[] };
  /** Offer a free-text note. */
  note?: {
    labelKey: string;
    placeholderKey?: string;
    required?: boolean;
    /** The note becomes required only while this reason code is selected (e.g. "Other"). */
    requiredWhenCode?: string | number;
  };
  /** Offer one integer quantity input per line. */
  lines?: { labelKey: string; items: readonly ReasonDialogLine[]; max?: number; errorKey: string };
}

export interface ReasonDialogResult {
  /** The chosen reason code, when `codes` was configured. */
  code?: string | number;
  /** The trimmed note, when `note` was configured. */
  note?: string;
  /** The entered quantities, when `lines` was configured. */
  lines?: { id: string; quantity: number }[];
}

/**
 * One dialog for every "confirm this with a reason" step: reject with a note, receive with quantities,
 * void/refund/waste with a reason code. Configure only the parts you need through `ReasonDialogData`.
 * Opened through `DialogService.openReasonDialog()`; resolves a `ReasonDialogResult`, or undefined on cancel.
 */
@Component({
  selector: 'cl-reason-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule,
    MatIconModule, MatInputModule, MatSelectModule, TranslatePipe,
  ],
  templateUrl: './reason.dialog.html',
  styleUrl: './reason.dialog.scss',
})
export class ReasonDialogComponent {
  readonly maxQuantity: number;
  form: FormGroup;

  constructor(
    fb: FormBuilder,
    private _dialogRef: MatDialogRef<ReasonDialogComponent, ReasonDialogResult | undefined>,
    @Inject(MAT_DIALOG_DATA) public data: ReasonDialogData,
  ) {
    this.maxQuantity = data.lines?.max ?? 100000;
    const controls: Record<string, unknown> = {};
    if (data.codes) {
      controls['code'] = [null, Validators.required];
    }
    if (data.note) {
      controls['note'] = ['', data.note.required ? [Validators.required, Validators.pattern(/\S/)] : []];
    }
    if (data.lines) {
      controls['lines'] = fb.array(data.lines.items.map(item => fb.group({
        quantity: [item.value, [Validators.required, Validators.min(0), Validators.max(this.maxQuantity), Validators.pattern(/^\d+$/)]],
      })));
    }
    this.form = fb.group(controls);

    const requiredWhenCode = data.note?.requiredWhenCode;
    if (data.codes && requiredWhenCode !== undefined) {
      const noteControl = this.form.controls['note'];
      this.form.controls['code'].valueChanges.subscribe(code => {
        noteControl.setValidators(code === requiredWhenCode ? [Validators.required, Validators.pattern(/\S/)] : []);
        noteControl.updateValueAndValidity();
      });
    }
  }

  lineGroup(index: number): FormGroup {
    return (this.form.controls['lines'] as FormArray).at(index) as FormGroup;
  }

  submit(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const result: ReasonDialogResult = {};
    if (this.data.codes) {
      result.code = this.form.value.code;
    }
    if (this.data.note) {
      result.note = ((this.form.value.note as string) ?? '').trim();
    }
    if (this.data.lines) {
      const rows = (this.form.controls['lines'] as FormArray).value as { quantity: number }[];
      result.lines = this.data.lines.items.map((item, i) => ({ id: item.id, quantity: Number(rows[i].quantity) }));
    }
    this._dialogRef.close(result);
  }

  cancel(): void {
    this._dialogRef.close(undefined);
  }
}

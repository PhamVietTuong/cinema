import { Component, Inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CinemaServiceAgent, TemplateChannelValues, showLoading, hideLoading, showSuccess, showException } from 'CinemaLib';

type Dto = CinemaServiceAgent.TemplateDTO;

export interface TemplateDialogData {
  template: Dto | null;
}

/** Tokens a booking-confirmation template body/subject can use — kept in sync with
 * BookingManager.BuildConfirmationValues (cinemabe). Shown as a hint, not translated (token
 * names are literal, not user-facing copy). */
const PLACEHOLDER_TOKENS = [
  'CustomerName', 'BookingCode', 'MovieTitle', 'TheaterName', 'RoomName', 'RoomTypeName',
  'ProjectionForm', 'ShowDate', 'ShowStartTime', 'ShowTime', 'TicketTypes', 'SeatLabels',
  'TicketCount', 'Concessions', 'SubTotal', 'DiscountAmount', 'TotalAmount', 'QrCodes', 'QrImages',
];

/** Create/edit form for a notification template, opened via MatDialog. Resolves `true` on save, `false` on cancel. */
@Component({
  selector: 'app-template-dialog',
  standalone: false,
  templateUrl: './template.dialog.html',
})
export class TemplateDialog {
  readonly editingId: string | null;
  readonly channelValues = TemplateChannelValues;
  readonly placeholderTokens = PLACEHOLDER_TOKENS;
  readonly channelEmail = CinemaServiceAgent.TemplateChannel.Email;
  form: FormGroup;

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _store: Store<any>,
    private _dialogRef: MatDialogRef<TemplateDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) data: TemplateDialogData,
  ) {
    const t = data.template;
    this.editingId = t?.id ?? null;
    this.form = this._fb.group({
      code: [{ value: t?.code ?? '', disabled: !!this.editingId }, Validators.required],
      name: [t?.name ?? '', Validators.required],
      channel: [t?.channel ?? this.channelEmail, Validators.required],
      subject: [t?.subject ?? ''],
      body: [t?.body ?? '', Validators.required],
      isActive: [t?.isActive ?? true],
    });

    this.form.get('channel')!.valueChanges.subscribe(() => this._updateSubjectValidator());
    this._updateSubjectValidator();
  }

  private _updateSubjectValidator(): void {
    const subject = this.form.get('subject')!;
    if (this.form.get('channel')!.value === this.channelEmail) {
      subject.setValidators([Validators.required]);
    } else {
      subject.clearValidators();
    }
    subject.updateValueAndValidity({ emitEvent: false });
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const obs = this.editingId
      ? this._svc.updateTemplate(CinemaServiceAgent.UpdateTemplateRequest.fromJS({ ...v, id: this.editingId }))
      : this._svc.createTemplate(CinemaServiceAgent.CreateTemplateRequest.fromJS(v));

    this._store.dispatch(showLoading());
    obs.subscribe({
      next: () => {
        this._store.dispatch(showSuccess({}));
        this._dialogRef.close(true);
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }

  cancel(): void {
    this._dialogRef.close(false);
  }
}

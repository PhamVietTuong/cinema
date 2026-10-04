import { ChangeDetectionStrategy, ChangeDetectorRef, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TranslateService } from '@ngx-translate/core';
import { ETicketChannelValues, SharedModule, StaffServiceAgent, apiErrorMessage } from 'CinemaLib';
import { MAX_RESENDS_PER_HOUR, resendAddressErrors } from './customer-service.logic';

export interface ResendETicketDialogData {
  invoice: StaffServiceAgent.CustomerInvoiceDTO;
  /** Resends left this hour according to the last answer (the full allowance when unknown). */
  remaining: number;
}

/**
 * Re-sends the e-ticket of an invoice by e-mail or SMS. The address is optional (the contact on file is used) but a walk-in
 * invoice without a contact needs one. Closes with the API result, or undefined on cancel; an API error stays in the dialog.
 */
@Component({
  selector: 'staff-resend-eticket-dialog',
  standalone: true,
  imports: [SharedModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
<div mat-dialog-title class="dialog-title">{{ 'customerService.resend.title' | translate: { code: data.invoice.code } }}</div>
<mat-dialog-content>
  <p class="hint">{{ 'customerService.resend.hint' | translate }}</p>
  <p class="quota" [class.quota--none]="data.remaining === 0">
    {{ 'customerService.resend.remaining' | translate: { remaining: data.remaining, max: max } }}
  </p>
  <form [formGroup]="form" class="fields">
    <mat-form-field appearance="outline" subscriptSizing="dynamic">
      <mat-label>{{ 'customerService.resend.channel' | translate }}</mat-label>
      <mat-select formControlName="channel">
        @for (option of channels; track option.value) {
          <mat-option [value]="option.value">{{ option.name | translate }}</mat-option>
        }
      </mat-select>
    </mat-form-field>
    <mat-form-field appearance="outline" subscriptSizing="dynamic">
      <mat-label>{{ 'customerService.resend.address' | translate }}</mat-label>
      <input matInput formControlName="address" autocomplete="off" maxlength="120">
      <mat-hint>{{ 'customerService.resend.addressHint' | translate }}</mat-hint>
    </mat-form-field>
  </form>
  @if (errors().length) {
    <p class="error">{{ errors()[0] | translate }}</p>
  }
  @if (serverError()) {
    <p class="error">{{ serverError() }}</p>
  }
</mat-dialog-content>
<div mat-dialog-actions class="dialog-actions">
  <button mat-button type="button" (click)="cancel()">{{ 'common.cancel' | translate }}</button>
  <button mat-raised-button color="primary" type="button" [disabled]="busy() || data.remaining === 0 || errors().length > 0" (click)="send()">
    <mat-icon>forward_to_inbox</mat-icon> {{ 'customerService.resend.send' | translate }}
  </button>
</div>
`,
  styles: [`
    .hint { color: var(--ml-muted); margin: 0 0 8px; }
    .quota { margin: 0 0 12px; font-weight: 600; }
    .quota--none { color: var(--ml-warn-ink); }
    .fields { display: flex; flex-direction: column; gap: 12px; min-width: 320px; }
    .error { color: var(--ml-warn-ink); margin: 8px 0 0; }
  `],
})
export class ResendETicketDialogComponent {
  readonly data = inject<ResendETicketDialogData>(MAT_DIALOG_DATA);
  private readonly _ref = inject(MatDialogRef<ResendETicketDialogComponent, StaffServiceAgent.ResendETicketResultDTO>);
  private readonly _api = inject(StaffServiceAgent.CustomerServiceHttpService);
  private readonly _translate = inject(TranslateService);
  private readonly _cdr = inject(ChangeDetectorRef);

  readonly channels = ETicketChannelValues;
  readonly max = MAX_RESENDS_PER_HOUR;
  readonly form = inject(FormBuilder).group({
    channel: [StaffServiceAgent.ETicketChannel.Email as StaffServiceAgent.ETicketChannel],
    address: [''],
  });
  readonly busy = signal(false);
  readonly serverError = signal('');
  private readonly _version = signal(0);

  readonly errors = computed(() => {
    this._version();
    const { channel, address } = this.form.getRawValue();
    return resendAddressErrors(channel, address);
  });

  constructor() {
    this.form.valueChanges.subscribe(() => {
      this._version.update(v => v + 1);
      this.serverError.set('');
    });
  }

  cancel(): void {
    this._ref.close();
  }

  send(): void {
    const { channel, address } = this.form.getRawValue();
    this.busy.set(true);
    this._api.resendETicket(StaffServiceAgent.ResendETicketRequest.fromJS({
      theaterId: this.data.invoice.theaterId ?? undefined,
      invoiceId: this.data.invoice.id,
      channel,
      address: (address ?? '').trim() || undefined,
    })).subscribe({
      next: result => this._ref.close(result),
      error: error => {
        this.busy.set(false);
        this.serverError.set(apiErrorMessage(error, this._translate.instant('customerService.resend.failed')));
        this._cdr.markForCheck();
      },
    });
  }
}

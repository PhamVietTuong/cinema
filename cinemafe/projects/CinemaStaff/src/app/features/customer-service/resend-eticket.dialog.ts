import { ChangeDetectionStrategy, ChangeDetectorRef, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TranslateService } from '@ngx-translate/core';
import { ETicketChannelValues, SharedModule, CinemaServiceAgent, apiErrorMessage } from 'CinemaLib';
import { MAX_RESENDS_PER_HOUR, resendAddressErrors } from './customer-service.logic';

export interface ResendETicketDialogData {
  invoice: CinemaServiceAgent.CustomerInvoiceDTO;
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
  templateUrl: './resend-eticket.dialog.html',
  styleUrl: './resend-eticket.dialog.scss',
})
export class ResendETicketDialogComponent {
  readonly data = inject<ResendETicketDialogData>(MAT_DIALOG_DATA);
  private readonly _ref = inject(MatDialogRef<ResendETicketDialogComponent, CinemaServiceAgent.ResendETicketResultDTO>);
  private readonly _api = inject(CinemaServiceAgent.HttpService);
  private readonly _translate = inject(TranslateService);
  private readonly _cdr = inject(ChangeDetectorRef);

  readonly channels = ETicketChannelValues;
  readonly max = MAX_RESENDS_PER_HOUR;
  readonly form = inject(FormBuilder).group({
    channel: [CinemaServiceAgent.ETicketChannel.Email as CinemaServiceAgent.ETicketChannel],
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
    this._api.resendETicket(CinemaServiceAgent.ResendETicketRequest.fromJS({
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

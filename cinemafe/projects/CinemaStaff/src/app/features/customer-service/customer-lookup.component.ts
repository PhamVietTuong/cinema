import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import {
  EmptyStateComponent, SharedModule, CinemaServiceAgent, StatusPillComponent, ToastService, hideLoading, showException, showLoading,
} from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';
import { ComplaintDialogComponent, ComplaintDialogData } from './complaint.dialog';
import { MAX_RESENDS_PER_HOUR, ResendQuota, resendExhausted, resendRemaining } from './customer-service.logic';
import { ResendETicketDialogComponent, ResendETicketDialogData } from './resend-eticket.dialog';

/**
 * Customer lookup for the service desk: an e-mail, a phone number or an invoice code finds the customer card (tier, points,
 * masked contact) and their latest invoices. From an invoice the desk can re-send the e-ticket (3 per hour) or file a complaint
 * prefilled with the customer and the invoice.
 */
@Component({
  selector: 'staff-customer-lookup',
  standalone: true,
  imports: [SharedModule, RouterLink, StatusPillComponent, EmptyStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './customer-lookup.component.html',
  styleUrl: './customer-lookup.component.scss',
})
export class CustomerLookupComponent {
  private readonly _api = inject(CinemaServiceAgent.HttpService);
  private readonly _dialog = inject(MatDialog);
  private readonly _store = inject(Store);
  private readonly _translate = inject(TranslateService);
  private readonly _toast = inject(ToastService);
  private readonly _theater = inject(TheaterContextService);

  readonly max = MAX_RESENDS_PER_HOUR;
  readonly paid = CinemaServiceAgent.InvoiceStatus.Paid;
  readonly query = signal('');
  readonly loading = signal(false);
  readonly result = signal<CinemaServiceAgent.CustomerLookupDTO | null>(null);
  private readonly _quotas = signal<Record<string, ResendQuota>>({});

  remaining(invoice: CinemaServiceAgent.CustomerInvoiceDTO): number {
    return resendRemaining(this._quotas()[invoice.id ?? ''], Date.now());
  }

  exhausted(invoice: CinemaServiceAgent.CustomerInvoiceDTO): boolean {
    return resendExhausted(this._quotas()[invoice.id ?? ''], Date.now());
  }

  search(): void {
    const query = this.query().trim();
    if (!query) {
      return;
    }
    this.loading.set(true);
    this._store.dispatch(showLoading());
    this._api.lookupCustomer(CinemaServiceAgent.LookupCustomerRequest.fromJS({ query })).subscribe({
      next: result => this.result.set(result),
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this.loading.set(false);
      this._store.dispatch(hideLoading());
    });
  }

  resend(invoice: CinemaServiceAgent.CustomerInvoiceDTO): void {
    const data: ResendETicketDialogData = { invoice, remaining: this.remaining(invoice) };
    this._dialog.open<ResendETicketDialogComponent, ResendETicketDialogData, CinemaServiceAgent.ResendETicketResultDTO>(
      ResendETicketDialogComponent, { width: '460px', maxWidth: '95vw', data },
    ).afterClosed().subscribe(sent => {
      if (!sent) {
        return;
      }
      this._quotas.update(q => ({ ...q, [invoice.id ?? '']: { remaining: sent.remainingThisHour ?? 0, at: Date.now() } }));
      this._toast.success(this._translate.instant('customerService.resend.success', {
        code: sent.invoiceCode, address: sent.maskedAddress, remaining: sent.remainingThisHour ?? 0,
      }));
    });
  }

  newComplaint(): void {
    this._openComplaint({ customer: this.result()?.customer ?? null });
  }

  complaintFor(invoice: CinemaServiceAgent.CustomerInvoiceDTO): void {
    this._openComplaint({
      customer: this.result()?.customer ?? null,
      invoice: { id: invoice.id ?? '', code: invoice.code ?? '' },
      theaterId: invoice.theaterId ?? undefined,
    });
  }

  private _openComplaint(extra: Partial<ComplaintDialogData>): void {
    const data: ComplaintDialogData = { theaterId: this._theater.isAdmin() ? (this._theater.currentTheaterId() ?? undefined) : undefined, ...extra };
    this._dialog.open<ComplaintDialogComponent, ComplaintDialogData, CinemaServiceAgent.ComplaintDTO>(
      ComplaintDialogComponent, { width: '520px', maxWidth: '95vw', data },
    ).afterClosed().subscribe(complaint => {
      if (complaint) {
        this._toast.success(this._translate.instant('customerService.complaint.created'));
      }
    });
  }
}

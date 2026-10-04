import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import {
  EmptyStateComponent, SharedModule, StaffServiceAgent, StatusPillComponent, ToastService, hideLoading, showException, showLoading,
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
  template: `
<div class="ad-page">
  <div class="ad-page-header">
    <div>
      <h1 class="ad-h1">{{ 'customerService.lookup.title' | translate }}</h1>
      <p class="ad-sub">{{ 'customerService.lookup.subtitle' | translate }}</p>
    </div>
    <div class="ad-toolbar">
      <a mat-stroked-button routerLink="/customer-service/complaints"><mat-icon>report_problem</mat-icon> {{ 'customerService.nav.complaints' | translate }}</a>
      <button mat-raised-button color="primary" type="button" (click)="newComplaint()">
        <mat-icon>add</mat-icon> {{ 'customerService.complaint.new' | translate }}
      </button>
    </div>
  </div>

  <mat-card class="search">
    <mat-form-field appearance="outline" subscriptSizing="dynamic" class="query">
      <mat-label>{{ 'customerService.lookup.query' | translate }}</mat-label>
      <input matInput [value]="query()" (input)="query.set($any($event.target).value)" (keydown.enter)="search()" autocomplete="off">
      <mat-hint>{{ 'customerService.lookup.queryHint' | translate }}</mat-hint>
    </mat-form-field>
    <button mat-raised-button color="primary" type="button" [disabled]="loading() || !query().trim()" (click)="search()">
      <mat-icon>search</mat-icon> {{ 'customerService.lookup.search' | translate }}
    </button>
  </mat-card>

  @if (result(); as r) {
    @if (r.customer; as c) {
      <mat-card class="customer">
        <div class="avatar"><mat-icon>person</mat-icon></div>
        <div class="info">
          <h2 class="name">{{ c.name }}</h2>
          <div class="muted">{{ c.maskedEmail }} {{ c.maskedPhone }}</div>
        </div>
        <div class="stats">
          @if (c.membershipName) {
            <span class="ad-pill ad-pill--violet">{{ c.membershipName }}</span>
          } @else {
            <span class="ad-pill ad-pill--neutral">{{ 'customerService.lookup.noTier' | translate }}</span>
          }
          <span class="points">{{ 'customerService.lookup.points' | translate: { points: c.points ?? 0 } }}</span>
        </div>
      </mat-card>
    } @else if (!(r.invoices?.length)) {
      <mat-card class="ad-card--pad-0">
        <cl-empty-state icon="person_search" messageKey="customerService.lookup.notFound" />
      </mat-card>
    }

    @if (r.invoices?.length) {
      <mat-card class="ad-card--pad-0">
        <div class="ad-table-wrap">
          <table class="ad-table">
            <thead>
              <tr>
                <th>{{ 'customerService.invoices.code' | translate }}</th>
                <th>{{ 'customerService.invoices.theater' | translate }}</th>
                <th>{{ 'customerService.invoices.movie' | translate }}</th>
                <th>{{ 'customerService.invoices.showtime' | translate }}</th>
                <th class="num">{{ 'customerService.invoices.amount' | translate }}</th>
                <th>{{ 'common.status' | translate }}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (invoice of r.invoices; track invoice.id) {
                <tr>
                  <td><strong>{{ invoice.code }}</strong><br><span class="muted">{{ invoice.creationTime | date: 'dd/MM/yyyy HH:mm' }}</span></td>
                  <td>{{ invoice.theaterName }}</td>
                  <td>{{ invoice.movieTitle }}</td>
                  <td>{{ invoice.firstShowStart | date: 'HH:mm dd/MM/yyyy' }}</td>
                  <td class="num">{{ invoice.finalAmount | currency: 'VND':'symbol':'1.0-0' }}</td>
                  <td><cl-status-pill kind="invoice" [value]="invoice.status" /></td>
                  <td class="actions">
                    @if (invoice.status === paid) {
                      <button mat-stroked-button type="button" [disabled]="exhausted(invoice)" (click)="resend(invoice)"
                              [matTooltip]="(exhausted(invoice) ? 'customerService.resend.exhausted' : 'customerService.resend.tip') | translate">
                        <mat-icon>forward_to_inbox</mat-icon> {{ 'customerService.resend.button' | translate }}
                        <span class="left">{{ remaining(invoice) }}/{{ max }}</span>
                      </button>
                    }
                    <button mat-icon-button type="button" (click)="complaintFor(invoice)" [matTooltip]="'customerService.complaint.fileFor' | translate">
                      <mat-icon>report_problem</mat-icon>
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </mat-card>
    }
  } @else if (!loading()) {
    <mat-card class="ad-card--pad-0">
      <cl-empty-state icon="person_search" messageKey="customerService.lookup.hint" />
    </mat-card>
  }
</div>
`,
  styles: [`
    .search { display: flex; gap: 12px; align-items: flex-start; padding: 16px; margin-bottom: 16px; }
    .query { flex: 1; }
    .customer { display: flex; align-items: center; gap: 16px; padding: 16px 24px; margin-bottom: 16px; }
    .avatar { width: 48px; height: 48px; border-radius: 50%; display: grid; place-items: center; background: var(--ml-action-soft); }
    .info { flex: 1; }
    .name { margin: 0; font-size: 1.1rem; }
    .stats { display: flex; align-items: center; gap: 12px; }
    .points { font-weight: 600; }
    .muted { color: var(--ml-muted); font-size: 0.85rem; }
    .num { text-align: right; }
    .actions { white-space: nowrap; text-align: right; }
    .left { margin-left: 6px; color: var(--ml-muted); font-size: 0.75rem; }
  `],
})
export class CustomerLookupComponent {
  private readonly _api = inject(StaffServiceAgent.CustomerServiceHttpService);
  private readonly _dialog = inject(MatDialog);
  private readonly _store = inject(Store);
  private readonly _translate = inject(TranslateService);
  private readonly _toast = inject(ToastService);
  private readonly _theater = inject(TheaterContextService);

  readonly max = MAX_RESENDS_PER_HOUR;
  readonly paid = StaffServiceAgent.InvoiceStatus.Paid;
  readonly query = signal('');
  readonly loading = signal(false);
  readonly result = signal<StaffServiceAgent.CustomerLookupDTO | null>(null);
  private readonly _quotas = signal<Record<string, ResendQuota>>({});

  remaining(invoice: StaffServiceAgent.CustomerInvoiceDTO): number {
    return resendRemaining(this._quotas()[invoice.id ?? ''], Date.now());
  }

  exhausted(invoice: StaffServiceAgent.CustomerInvoiceDTO): boolean {
    return resendExhausted(this._quotas()[invoice.id ?? ''], Date.now());
  }

  search(): void {
    const query = this.query().trim();
    if (!query) {
      return;
    }
    this.loading.set(true);
    this._store.dispatch(showLoading());
    this._api.lookupCustomer(StaffServiceAgent.LookupCustomerRequest.fromJS({ query })).subscribe({
      next: result => this.result.set(result),
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this.loading.set(false);
      this._store.dispatch(hideLoading());
    });
  }

  resend(invoice: StaffServiceAgent.CustomerInvoiceDTO): void {
    const data: ResendETicketDialogData = { invoice, remaining: this.remaining(invoice) };
    this._dialog.open<ResendETicketDialogComponent, ResendETicketDialogData, StaffServiceAgent.ResendETicketResultDTO>(
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

  complaintFor(invoice: StaffServiceAgent.CustomerInvoiceDTO): void {
    this._openComplaint({
      customer: this.result()?.customer ?? null,
      invoice: { id: invoice.id ?? '', code: invoice.code ?? '' },
      theaterId: invoice.theaterId ?? undefined,
    });
  }

  private _openComplaint(extra: Partial<ComplaintDialogData>): void {
    const data: ComplaintDialogData = { theaterId: this._theater.isAdmin() ? (this._theater.currentTheaterId() ?? undefined) : undefined, ...extra };
    this._dialog.open<ComplaintDialogComponent, ComplaintDialogData, StaffServiceAgent.ComplaintDTO>(
      ComplaintDialogComponent, { width: '520px', maxWidth: '95vw', data },
    ).afterClosed().subscribe(complaint => {
      if (complaint) {
        this._toast.success(this._translate.instant('customerService.complaint.created'));
      }
    });
  }
}

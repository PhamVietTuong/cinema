import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import {
  EmptyStateComponent,
  SharedModule,
  CinemaServiceAgent,
  StatusPillComponent,
  hideLoading,
  showException,
  showLoading,
} from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';
import { buildScanRequest, isAdmitted, needsAgePrompt } from './gate-scan.state';

type InvoiceStatus = 'unused' | 'partial' | 'used';

/** Pill shown on each booking, derived from how many of its tickets are already used. */
const INVOICE_STATUS_SPEC: Record<InvoiceStatus, { labelKey: string; pillClass: string }> = {
  unused: { labelKey: 'gate.lookup.unused', pillClass: 'ad-pill--success' },
  partial: { labelKey: 'gate.lookup.partlyUsed', pillClass: 'ad-pill--warn' },
  used: { labelKey: 'gate.lookup.used', pillClass: 'ad-pill--neutral' },
};

/**
 * Gate lookup: find today's paid tickets by invoice code or phone number and admit them one by one
 * when the patron has no scannable QR. "Admit" is a Gate/Scan with that ticket's QR code.
 */
@Component({
  selector: 'staff-gate-lookup',
  standalone: true,
  imports: [SharedModule, StatusPillComponent, EmptyStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
<h2 class="ad-card-title">{{ 'gate.lookup.title' | translate }}</h2>
<form class="lookup-form" [formGroup]="form" (ngSubmit)="search()">
  <div class="ad-field">
    <label class="ad-label" for="gate-lookup-invoice">{{ 'gate.lookup.invoiceCode' | translate }}</label>
    <input id="gate-lookup-invoice" class="ad-input" formControlName="invoiceCode" autocomplete="off">
  </div>
  <div class="or">{{ 'gate.lookup.or' | translate }}</div>
  <div class="ad-field">
    <label class="ad-label" for="gate-lookup-phone">{{ 'gate.lookup.phone' | translate }}</label>
    <input id="gate-lookup-phone" class="ad-input" formControlName="phone" inputmode="tel" autocomplete="off">
  </div>
  <button class="ad-btn ad-btn--primary ad-btn--block" type="submit" [disabled]="loading()">
    <mat-icon>search</mat-icon> {{ 'gate.lookup.search' | translate }}
  </button>
</form>
<p class="hint">{{ 'gate.lookup.hint' | translate }}</p>

@for (invoice of invoices(); track invoice.invoiceCode) {
  @let status = statusSpec(invoice);
  <div class="invoice">
    <div class="invoice-head">
      <div>
        <strong>{{ invoice.invoiceCode }}</strong>
        <small class="muted">{{ invoice.customerName }} &middot; {{ invoice.maskedPhone }} &middot; {{ 'gate.lookup.ticketCount' | translate: { count: (invoice.tickets ?? []).length } }}</small>
      </div>
      <span [class]="'ad-pill ' + status.pillClass">{{ status.labelKey | translate }}</span>
    </div>
      @for (ticket of invoice.tickets ?? []; track ticket.qrCode) {
        <div class="ticket-row">
          <strong class="seat">{{ ticket.seatLabel }}</strong>
          <span class="info">{{ ticket.movieTitle }} &middot; {{ ticket.roomName }} &middot; {{ ticket.showTime | date: 'HH:mm dd/MM' }}</span>
          @if (outcomeOf(ticket.qrCode); as outcome) {
            <cl-status-pill kind="scanOutcome" [value]="outcome.outcome" />
          } @else if (ticket.isUsed) {
            <span class="ad-pill ad-pill--neutral">{{ 'gate.lookup.used' | translate }}</span>
          }
          <button mat-flat-button color="primary" type="button"
                  [disabled]="!canAdmit(ticket)"
                  (click)="admit(ticket)">
            {{ (needsAge(ticket.qrCode) ? 'gate.confirmAge' : 'gate.lookup.admit') | translate }}
          </button>
        </div>
      }
  </div>
} @empty {
  @if (searched()) {
    <cl-empty-state icon="confirmation_number" messageKey="gate.lookup.empty" />
  }
}
`,
  styles: [`
    .lookup-form { display: flex; flex-direction: column; gap: 4px; }
    .or { display: flex; align-items: center; gap: 10px; color: var(--ml-muted); font-size: 0.8rem; margin: 8px 0; }
    .or::before, .or::after { content: ''; flex: 1; height: 1px; background: var(--ml-rule); }
    .lookup-form button { margin-top: 12px; }
    .hint, .muted { color: var(--ml-muted); }
    .hint { font-size: 0.85rem; margin: 10px 0 0; }
    .invoice { border: 1px solid var(--ml-rule); border-radius: 10px; padding: 12px; margin-top: 10px; }
    .invoice-head { display: flex; justify-content: space-between; align-items: center; gap: 12px; margin-bottom: 8px; }
    .invoice-head small { display: block; }
    .ticket-row { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; padding: 8px 0; border-top: 1px solid var(--ml-panel-3, rgba(0, 0, 0, 0.08)); }
    .seat { min-width: 48px; }
    .info { flex: 1 1 160px; }
  `],
})
export class GateLookupComponent {
  private readonly _gate = inject(CinemaServiceAgent.HttpService);
  private readonly _theater = inject(TheaterContextService);
  private readonly _store = inject(Store);
  private readonly _fb = inject(FormBuilder);

  readonly theaterId = this._theater.currentTheaterId;
  readonly form = this._fb.group({
    invoiceCode: ['', [Validators.maxLength(50)]],
    phone: ['', [Validators.maxLength(20)]],
  });

  readonly invoices = signal<CinemaServiceAgent.GateLookupResultDTO[]>([]);
  readonly loading = signal(false);
  readonly searched = signal(false);
  /** Latest admit result per ticket QR code. */
  private readonly _outcomes = signal<Record<string, CinemaServiceAgent.ScanTicketResultDTO>>({});

  outcomeOf(qrCode?: string): CinemaServiceAgent.ScanTicketResultDTO | null {
    return this._outcomes()[qrCode ?? ''] ?? null;
  }

  /** Pill spec of a booking: used once every ticket is used or admitted during this lookup. */
  statusSpec(invoice: CinemaServiceAgent.GateLookupResultDTO): { labelKey: string; pillClass: string } {
    const tickets = invoice.tickets ?? [];
    const usedCount = tickets.filter(ticket => ticket.isUsed || isAdmitted(this.outcomeOf(ticket.qrCode))).length;
    if (usedCount === 0) {
      return INVOICE_STATUS_SPEC.unused;
    }
    if (usedCount === tickets.length) {
      return INVOICE_STATUS_SPEC.used;
    }
    return INVOICE_STATUS_SPEC.partial;
  }

  needsAge(qrCode?: string): boolean {
    return needsAgePrompt(this.outcomeOf(qrCode));
  }

  /** A ticket can be admitted until it is used or an admit already succeeded. */
  canAdmit(ticket: CinemaServiceAgent.GateLookupTicketDTO): boolean {
    return !ticket.isUsed && !isAdmitted(this.outcomeOf(ticket.qrCode)) && !this.loading();
  }

  search(): void {
    const value = this.form.value;
    const invoiceCode = (value.invoiceCode ?? '').trim();
    const phone = (value.phone ?? '').trim();
    if (!invoiceCode && !phone) {
      return;
    }
    this.loading.set(true);
    this._store.dispatch(showLoading());
    this._gate.lookup(CinemaServiceAgent.GateLookupRequest.fromJS({
      invoiceCode: invoiceCode || undefined,
      phone: phone || undefined,
      theaterId: this.theaterId() ?? undefined,
    })).subscribe({
      next: invoices => {
        this.invoices.set(invoices ?? []);
        this._outcomes.set({});
        this.searched.set(true);
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this.loading.set(false);
      this._store.dispatch(hideLoading());
    });
  }

  admit(ticket: CinemaServiceAgent.GateLookupTicketDTO): void {
    const code = ticket.qrCode ?? '';
    if (!code) {
      return;
    }
    const ageConfirmed = this.needsAge(code);
    this._gate.scan(buildScanRequest(code, { theaterId: this.theaterId(), ageConfirmed })).subscribe({
      next: result => this._outcomes.update(map => ({ ...map, [code]: result })),
      error: error => this._store.dispatch(showException({ error })),
    });
  }
}

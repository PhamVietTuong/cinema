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
  templateUrl: './gate-lookup.component.html',
  styleUrl: './gate-lookup.component.scss',
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

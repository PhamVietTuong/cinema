import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import {
  EmptyStateComponent,
  SharedModule,
  StaffServiceAgent,
  StatusPillComponent,
  hideLoading,
  showException,
  showLoading,
} from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';
import { buildScanRequest, isAdmitted, needsAgePrompt } from './gate-scan.state';

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
@if (!theaterId()) {
  <mat-card class="ad-card--pad-0">
    <cl-empty-state icon="theaters" messageKey="gate.pickTheater" hintKey="gate.pickTheaterHint" />
  </mat-card>
} @else {
  <form class="ad-card lookup-form" [formGroup]="form" (ngSubmit)="search()">
    <mat-form-field appearance="outline">
      <mat-label>{{ 'gate.lookup.invoiceCode' | translate }}</mat-label>
      <input matInput formControlName="invoiceCode" autocomplete="off">
    </mat-form-field>
    <mat-form-field appearance="outline">
      <mat-label>{{ 'gate.lookup.phone' | translate }}</mat-label>
      <input matInput formControlName="phone" inputmode="tel" autocomplete="off">
    </mat-form-field>
    <button mat-raised-button color="primary" type="submit" [disabled]="loading()">
      <mat-icon>search</mat-icon> {{ 'gate.lookup.search' | translate }}
    </button>
  </form>
  <p class="hint">{{ 'gate.lookup.hint' | translate }}</p>

  @for (invoice of invoices(); track invoice.invoiceCode) {
    <mat-card class="invoice">
      <div class="invoice-head">
        <strong>{{ invoice.invoiceCode }}</strong>
        <span>{{ invoice.customerName }}</span>
        <span class="muted">{{ invoice.maskedPhone }}</span>
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
    </mat-card>
  } @empty {
    @if (searched()) {
      <mat-card class="ad-card--pad-0"><cl-empty-state icon="confirmation_number" messageKey="gate.lookup.empty" /></mat-card>
    }
  }
}
`,
  styles: [`
    .lookup-form { display: flex; gap: 12px; align-items: flex-start; flex-wrap: wrap; padding: 16px 24px 0; }
    .hint, .muted { color: var(--ml-muted); }
    .invoice { margin-bottom: 12px; padding: 12px 16px; }
    .invoice-head { display: flex; gap: 16px; align-items: baseline; flex-wrap: wrap; margin-bottom: 8px; }
    .ticket-row { display: flex; gap: 12px; align-items: center; padding: 8px 0; border-top: 1px solid var(--ml-panel-3, rgba(0, 0, 0, 0.08)); }
    .seat { min-width: 48px; }
    .info { flex: 1; }
  `],
})
export class GateLookupComponent {
  private readonly _gate = inject(StaffServiceAgent.GateHttpService);
  private readonly _theater = inject(TheaterContextService);
  private readonly _store = inject(Store);
  private readonly _fb = inject(FormBuilder);

  readonly theaterId = this._theater.currentTheaterId;
  readonly form = this._fb.group({
    invoiceCode: ['', [Validators.maxLength(50)]],
    phone: ['', [Validators.maxLength(20)]],
  });

  readonly invoices = signal<StaffServiceAgent.GateLookupResultDTO[]>([]);
  readonly loading = signal(false);
  readonly searched = signal(false);
  /** Latest admit result per ticket QR code. */
  private readonly _outcomes = signal<Record<string, StaffServiceAgent.ScanTicketResultDTO>>({});

  outcomeOf(qrCode?: string): StaffServiceAgent.ScanTicketResultDTO | null {
    return this._outcomes()[qrCode ?? ''] ?? null;
  }

  needsAge(qrCode?: string): boolean {
    return needsAgePrompt(this.outcomeOf(qrCode));
  }

  /** A ticket can be admitted until it is used or an admit already succeeded. */
  canAdmit(ticket: StaffServiceAgent.GateLookupTicketDTO): boolean {
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
    this._gate.lookup(StaffServiceAgent.GateLookupRequest.fromJS({
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

  admit(ticket: StaffServiceAgent.GateLookupTicketDTO): void {
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

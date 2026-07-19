import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { SharedModule, PaymentServiceAgent } from 'CinemaLib';
import { TranslateService } from '@ngx-translate/core';
import * as QRCode from 'qrcode';

type ReturnState = 'verifying' | 'success' | 'pending';

@Component({
  selector: 'app-payment-return',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './payment-return.component.html',
  styleUrl: './payment-return.component.scss',
})
export class PaymentReturnComponent implements OnInit, OnDestroy {
  /** How long, and how often, we re-check the invoice before giving up. */
  private static readonly POLL_INTERVAL_MS = 2000;
  private static readonly MAX_ATTEMPTS = 15; // ~30s total

  readonly InvoiceStatus = PaymentServiceAgent.InvoiceStatus;

  state: ReturnState = 'verifying';
  invoiceId = '';
  invoice: PaymentServiceAgent.InvoiceDTO | null = null;
  bookingCode = '';
  qrDataUrl = '';

  private _attempts = 0;
  private _timer: any = null;

  constructor(
    private _route: ActivatedRoute,
    private _router: Router,
    private _payment: PaymentServiceAgent.HttpService,
    private _cdr: ChangeDetectorRef,
    private _translate: TranslateService,
  ) {}

  ngOnInit(): void {
    this.invoiceId = this._route.snapshot.queryParamMap.get('invoiceId') ?? '';
    if (!this.invoiceId) {
      // Nothing to verify against — send the user to their bookings.
      this.state = 'pending';
      this._cdr.markForCheck();
      return;
    }
    this._poll();
  }

  ngOnDestroy(): void {
    if (this._timer) {
      clearTimeout(this._timer);
    }
  }

  private _poll(): void {
    this._attempts++;
    this._payment.getInvoice(this.invoiceId).subscribe({
      next: inv => {
        this.invoice = inv;
        if (inv?.status === this.InvoiceStatus.Paid) {
          this._onPaid(inv);
          return;
        }
        this._scheduleNext();
      },
      error: () => { this._scheduleNext(); },
    });
  }

  private _scheduleNext(): void {
    if (this._attempts >= PaymentReturnComponent.MAX_ATTEMPTS) {
      // Still not confirmed within the window — treat as pending/failed.
      this.state = 'pending';
      this._cdr.markForCheck();
      return;
    }
    this._timer = setTimeout(() => { this._poll(); }, PaymentReturnComponent.POLL_INTERVAL_MS);
  }

  private _onPaid(inv: PaymentServiceAgent.InvoiceDTO): void {
    this.state = 'success';
    this.bookingCode = inv.code ?? inv.id ?? '';
    this._cdr.markForCheck();
    if (this.bookingCode) {
      QRCode.toDataURL(this.bookingCode, { margin: 1, width: 200 })
        .then(url => { this.qrDataUrl = url; this._cdr.markForCheck(); })
        .catch(() => { /* keep the icon fallback */ });
    }
  }

  get seatLabels(): string[] {
    return (this.invoice?.tickets ?? []).map(t => t.seatLabel).filter((s): s is string => !!s);
  }

  goProfile(): void {
    this._router.navigate(['/profile']);
  }

  goHome(): void {
    this._router.navigate(['/']);
  }
}

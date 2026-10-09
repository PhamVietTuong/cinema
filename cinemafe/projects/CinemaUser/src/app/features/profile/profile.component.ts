import { Component, inject } from '@angular/core';
import { ValidatorFn, Validators } from '@angular/forms';
import { SharedModule, IdentityServiceAgent, PaymentServiceAgent, CinemaServiceAgent, ToastService, ProfileFormBase } from 'CinemaLib';
import * as QRCode from 'qrcode';

/** Vietnamese phone number: leading 0 or +84 followed by 9–10 digits. */
const PHONE_PATTERN = /^(?:\+84|0)\d{9,10}$/;

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
})
export class ProfileComponent extends ProfileFormBase {
  private _payment = inject(PaymentServiceAgent.HttpService);
  private _cinema = inject(CinemaServiceAgent.HttpService);
  private _toast = inject(ToastService);

  avatarUploading = false;
  avatarErr = '';

  readonly InvoiceStatus = PaymentServiceAgent.InvoiceStatus;
  tab: 'overview' | 'bookings' | 'settings' = 'overview';

  /** Persisted email notification preferences (initialised from the profile). */
  notif = { booking: true, promos: false, reminders: true };
  notifSaving = false;

  readonly perks = [
    { icon: 'confirmation_number', text: 'profile.perkDiscount' },
    { icon: 'fastfood', text: 'profile.perkFreePopcorn' },
    { icon: 'star', text: 'profile.perkEarnPoints' },
  ];

  invoices: PaymentServiceAgent.InvoiceDTO[] = [];
  invoicesLoading = false;
  expandedId: string | null = null;
  /** Per-ticket e-ticket QR data URLs, keyed by the ticket's QR token. */
  qrMap: Record<string, string> = {};

  override ngOnInit(): void {
    super.ngOnInit();
    this.loadInvoices();
  }

  protected override _phoneValidators(): ValidatorFn[] {
    return [Validators.pattern(PHONE_PATTERN)];
  }

  protected override _profileI18n(): { updateSuccess: string; updateFailed: string } {
    return { updateSuccess: 'profile.updateSuccess', updateFailed: 'profile.updateFailed' };
  }

  protected override _initialsFallback(): string {
    return 'U';
  }

  protected override _onProfileLoaded(u: IdentityServiceAgent.UserDTO): void {
    this.notif = {
      booking: u.notifyBookingEmails ?? true,
      promos: u.notifyPromotionEmails ?? false,
      reminders: u.notifyReminderEmails ?? true,
    };
  }

  loadInvoices(): void {
    this.invoicesLoading = true;
    this._payment.getMyInvoices(PaymentServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 50 }))
      .subscribe({
        next: r => { this.invoices = r.results ?? []; this.invoicesLoading = false; this._buildQrCodes(); this._cdr.markForCheck(); },
        error: () => { this.invoicesLoading = false; this._cdr.markForCheck(); },
      });
  }

  /** Renders each paid ticket's QR token into a scannable image (one per seat). */
  private _buildQrCodes(): void {
    for (const inv of this.invoices) {
      if (inv.status !== this.InvoiceStatus.Paid) { continue; }
      for (const t of inv.tickets ?? []) {
        const code = t.qrCode;
        if (!code || this.qrMap[code]) { continue; }
        QRCode.toDataURL(code, { margin: 1, width: 140 })
          .then(url => { this.qrMap[code] = url; this._cdr.markForCheck(); })
          .catch(() => { /* leave unset */ });
      }
    }
  }

  /** The full ticket list for an invoice id (for the expanded e-ticket panel). */
  ticketsOf(id?: string): PaymentServiceAgent.InvoiceTicketDTO[] {
    return this.invoices.find(i => i.id === id)?.tickets ?? [];
  }
  foodsOf(id?: string): PaymentServiceAgent.InvoiceFoodDTO[] {
    return this.invoices.find(i => i.id === id)?.foods ?? [];
  }
  isPaid(status?: PaymentServiceAgent.InvoiceStatus): boolean {
    return status === this.InvoiceStatus.Paid;
  }

  onPickAvatar(e: Event): void {
    const file = (e.target as HTMLInputElement).files?.[0];
    if (!file) { return; }
    this.avatarUploading = true; this.avatarErr = '';
    this._cinema.uploadImage({ data: file, fileName: file.name }).subscribe({
      next: r => { this.profileForm.patchValue({ avatar: r.url ?? '' }); this.avatarUploading = false; this._cdr.markForCheck(); },
      error: () => { this.avatarErr = this._translate.instant('profile.uploadFailed'); this.avatarUploading = false; this._cdr.markForCheck(); },
    });
  }

  saveNotifications(): void {
    this.notifSaving = true;
    this._identity.updateNotificationPreferences(IdentityServiceAgent.UpdateNotificationPreferencesRequest.fromJS({
      notifyBookingEmails: this.notif.booking,
      notifyPromotionEmails: this.notif.promos,
      notifyReminderEmails: this.notif.reminders,
    })).subscribe({
      next: () => {
        this.notifSaving = false;
        this._toast.success(this._translate.instant('profile.notifSaveSuccess'));
        this._cdr.markForCheck();
      },
      error: e => {
        this.notifSaving = false;
        this._toast.error(this._err(e, this._translate.instant('profile.notifSaveFailed')));
        this._cdr.markForCheck();
      },
    });
  }

  get bookingCount(): number { return this.invoices.length; }
  get ticketCount(): number { return this.invoices.reduce((n, i) => n + (i.tickets?.length ?? 0), 0); }

  /** Top recent bookings flattened for the dashboard table. */
  get recentBookings(): { movie: string; date?: Date; seats: string; status?: PaymentServiceAgent.InvoiceStatus }[] {
    return this.invoices.slice(0, 5).map(inv => ({
      movie: inv.tickets?.[0]?.movieTitle ?? '—',
      date: inv.tickets?.[0]?.showTime,
      seats: (inv.tickets ?? []).map(t => t.seatLabel).filter(Boolean).join(', ') || '—',
      status: inv.status,
    }));
  }

  get totalSpent(): number {
    return this.invoices.filter(i => i.status === this.InvoiceStatus.Paid).reduce((s, i) => s + (i.finalAmount ?? 0), 0);
  }
  get upcomingCount(): number {
    const now = Date.now();
    return this.invoices.filter(i => (i.tickets ?? []).some(t => t.showTime && new Date(t.showTime).getTime() > now)).length;
  }
  /** All bookings flattened into table rows. */
  get bookingRows(): { id?: string; movie: string; date?: Date; seats: string; foods: string; total?: number; status?: PaymentServiceAgent.InvoiceStatus }[] {
    return this.invoices.map(inv => ({
      id: inv.id,
      movie: inv.tickets?.[0]?.movieTitle ?? '—',
      date: inv.tickets?.[0]?.showTime,
      seats: (inv.tickets ?? []).map(t => t.seatLabel).filter(Boolean).join(', ') || '—',
      foods: (inv.foods ?? []).map(f => `${f.quantity} × ${f.foodName}`).join(', ') || '—',
      total: inv.finalAmount,
      status: inv.status,
    }));
  }
  cancelById(id?: string): void {
    const inv = this.invoices.find(i => i.id === id);
    if (inv) { this.cancelBooking(inv); }
  }
  refundById(id?: string): void {
    const inv = this.invoices.find(i => i.id === id);
    if (inv) { this.refundBooking(inv); }
  }

  toggleInvoice(id?: string): void { this.expandedId = this.expandedId === id ? null : (id ?? null); }

  cancelBooking(inv: PaymentServiceAgent.InvoiceDTO): void {
    if (!inv.id || !confirm(this._translate.instant('profile.confirmCancelBooking'))) { return; }
    this._payment.cancelBooking(PaymentServiceAgent.CancelBookingRequest.fromJS({ invoiceId: inv.id }))
      .subscribe({ next: () => this.loadInvoices(), error: () => this.loadInvoices() });
  }

  refundBooking(inv: PaymentServiceAgent.InvoiceDTO): void {
    if (!inv.id || !confirm(this._translate.instant('profile.confirmRefundBooking'))) { return; }
    this._payment.refundBooking(PaymentServiceAgent.RefundBookingRequest.fromJS({ invoiceId: inv.id }))
      .subscribe({
        next: () => {
          this._toast.success(this._translate.instant('profile.refundSuccess'));
          this.loadInvoices();
        },
        error: e => {
          this._toast.error(this._err(e, this._translate.instant('profile.refundFailed')));
          this._cdr.markForCheck();
        },
      });
  }

  statusLabel(s?: PaymentServiceAgent.InvoiceStatus): string {
    switch (s) {
      case this.InvoiceStatus.Paid: return this._translate.instant('profile.statusPaid');
      case this.InvoiceStatus.Pending: return this._translate.instant('profile.statusPending');
      case this.InvoiceStatus.Cancelled: return this._translate.instant('profile.statusCancelled');
      case this.InvoiceStatus.Failed: return this._translate.instant('profile.statusFailed');
      case this.InvoiceStatus.Refunded: return this._translate.instant('profile.statusRefunded');
      default: return '—';
    }
  }
  statusClass(s?: PaymentServiceAgent.InvoiceStatus): string {
    switch (s) {
      case this.InvoiceStatus.Paid: return 'is-paid';
      case this.InvoiceStatus.Pending: return 'is-pending';
      case this.InvoiceStatus.Refunded: return 'is-refunded';
      default: return 'is-cancelled';
    }
  }
}

import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { SharedModule, IdentityServiceAgent, PaymentServiceAgent, CinemaServiceAgent, ToastService, profileUpdated } from 'CinemaLib';
import { TranslateService } from '@ngx-translate/core';
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
export class ProfileComponent implements OnInit {
  private _identity = inject(IdentityServiceAgent.HttpService);
  private _payment = inject(PaymentServiceAgent.HttpService);
  private _cinema = inject(CinemaServiceAgent.HttpService);
  private _fb = inject(FormBuilder);
  private _cdr = inject(ChangeDetectorRef);
  private _translate = inject(TranslateService);
  private _toast = inject(ToastService);
  private _store = inject(Store);

  avatarUploading = false;
  avatarErr = '';

  readonly InvoiceStatus = PaymentServiceAgent.InvoiceStatus;
  tab: 'overview' | 'bookings' | 'settings' = 'overview';

  /** Persisted email notification preferences (initialised from the profile). */
  notif = { booking: true, promos: false, reminders: true };
  notifSaving = false;

  readonly perks = [
    { icon: 'fa-ticket', text: 'profile.perkDiscount' },
    { icon: 'fa-bowl-food', text: 'profile.perkFreePopcorn' },
    { icon: 'fa-star', text: 'profile.perkEarnPoints' },
  ];

  user: IdentityServiceAgent.UserDTO | null = null;
  invoices: PaymentServiceAgent.InvoiceDTO[] = [];
  invoicesLoading = false;
  expandedId: string | null = null;
  /** Per-ticket e-ticket QR data URLs, keyed by the ticket's QR token. */
  qrMap: Record<string, string> = {};

  profileForm: FormGroup = this._fb.group({
    name: ['', Validators.required],
    phone: ['', Validators.pattern(PHONE_PATTERN)],
    avatar: [''],
  });
  passwordForm: FormGroup = this._fb.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(6)]],
    confirmNewPassword: ['', Validators.required],
  });

  profileMsg = ''; profileErr = '';
  passwordMsg = ''; passwordErr = '';

  ngOnInit(): void {
    this._identity.getProfile().subscribe({
      next: u => {
        this.user = u;
        this.profileForm.patchValue({ name: u.name ?? '', phone: u.phone ?? '', avatar: u.avatar ?? '' });
        this.notif = {
          booking: u.notifyBookingEmails ?? true,
          promos: u.notifyPromotionEmails ?? false,
          reminders: u.notifyReminderEmails ?? true,
        };
        this._cdr.markForCheck();
      },
      error: () => this._cdr.markForCheck(),
    });
    this.loadInvoices();
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
  isPaid(status?: PaymentServiceAgent.InvoiceStatus): boolean {
    return status === this.InvoiceStatus.Paid;
  }

  saveProfile(): void {
    if (this.profileForm.invalid) { this.profileForm.markAllAsTouched(); return; }
    this.profileMsg = ''; this.profileErr = '';
    this._identity.updateProfile(IdentityServiceAgent.UpdateProfileRequest.fromJS(this.profileForm.value))
      .subscribe({
        next: () => {
          this.profileMsg = this._translate.instant('profile.updateSuccess');
          this._identity.getProfile().subscribe(u => {
            this.user = u;
            // Refresh the cached auth user too, otherwise the header keeps showing the old
            // name — and keeps showing it after a reload, since storage still holds the old copy.
            this._store.dispatch(profileUpdated({ user: u }));
            this._cdr.markForCheck();
          });
          this._cdr.markForCheck();
        },
        error: e => { this.profileErr = this._err(e, this._translate.instant('profile.updateFailed')); this._cdr.markForCheck(); },
      });
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

  changePassword(): void {
    if (this.passwordForm.invalid) { this.passwordForm.markAllAsTouched(); return; }
    const v = this.passwordForm.value;
    this.passwordMsg = ''; this.passwordErr = '';
    if (v.newPassword !== v.confirmNewPassword) { this.passwordErr = this._translate.instant('profile.passwordMismatch'); return; }
    this._identity.changePassword(IdentityServiceAgent.ChangePasswordRequest.fromJS(v))
      .subscribe({
        next: () => { this.passwordMsg = this._translate.instant('profile.passwordChangeSuccess'); this.passwordForm.reset(); this._cdr.markForCheck(); },
        error: e => { this.passwordErr = this._err(e, this._translate.instant('profile.passwordChangeFailed')); this._cdr.markForCheck(); },
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
  get bookingRows(): { id?: string; movie: string; date?: Date; seats: string; total?: number; status?: PaymentServiceAgent.InvoiceStatus }[] {
    return this.invoices.map(inv => ({
      id: inv.id,
      movie: inv.tickets?.[0]?.movieTitle ?? '—',
      date: inv.tickets?.[0]?.showTime,
      seats: (inv.tickets ?? []).map(t => t.seatLabel).filter(Boolean).join(', ') || '—',
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

  initials(name?: string): string {
    const parts = (name ?? '').trim().split(/\s+/);
    return ((parts[0]?.[0] ?? '') + (parts[parts.length - 1]?.[0] ?? '')).toUpperCase() || 'U';
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

  private _err(e: any, fallback: string): string {
    const x = e?.error;
    return (typeof x === 'string' && x) ? x : (x?.error || x?.message || fallback);
  }
}

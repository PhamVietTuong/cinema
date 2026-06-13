import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { SharedModule, IdentityServiceAgent, PaymentServiceAgent } from 'CinemaLib';

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
  private _fb = inject(FormBuilder);
  private _cdr = inject(ChangeDetectorRef);

  readonly InvoiceStatus = PaymentServiceAgent.InvoiceStatus;
  tab: 'overview' | 'bookings' | 'settings' = 'overview';

  /** Static (non-persisted) notification preferences for the settings UI. */
  notif = { trailers: true, reminders: true, promos: false };

  readonly perks = [
    { icon: 'fa-ticket', text: 'Giảm 10% cho tất cả các vé' },
    { icon: 'fa-bowl-food', text: 'Bắp rang miễn phí hàng tháng' },
    { icon: 'fa-star', text: 'Tích điểm thưởng cho mỗi lần đặt vé' },
  ];

  user: IdentityServiceAgent.UserDTO | null = null;
  invoices: PaymentServiceAgent.InvoiceDTO[] = [];
  invoicesLoading = false;
  expandedId: string | null = null;

  profileForm: FormGroup = this._fb.group({
    name: ['', Validators.required],
    phone: [''],
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
        next: r => { this.invoices = r.results ?? []; this.invoicesLoading = false; this._cdr.markForCheck(); },
        error: () => { this.invoicesLoading = false; this._cdr.markForCheck(); },
      });
  }

  saveProfile(): void {
    if (this.profileForm.invalid) { this.profileForm.markAllAsTouched(); return; }
    this.profileMsg = ''; this.profileErr = '';
    this._identity.updateProfile(IdentityServiceAgent.UpdateProfileRequest.fromJS(this.profileForm.value))
      .subscribe({
        next: () => {
          this.profileMsg = 'Cập nhật thông tin thành công.';
          this._identity.getProfile().subscribe(u => { this.user = u; this._cdr.markForCheck(); });
          this._cdr.markForCheck();
        },
        error: e => { this.profileErr = this._err(e, 'Cập nhật thất bại.'); this._cdr.markForCheck(); },
      });
  }

  changePassword(): void {
    if (this.passwordForm.invalid) { this.passwordForm.markAllAsTouched(); return; }
    const v = this.passwordForm.value;
    this.passwordMsg = ''; this.passwordErr = '';
    if (v.newPassword !== v.confirmNewPassword) { this.passwordErr = 'Mật khẩu xác nhận không khớp.'; return; }
    this._identity.changePassword(IdentityServiceAgent.ChangePasswordRequest.fromJS(v))
      .subscribe({
        next: () => { this.passwordMsg = 'Đổi mật khẩu thành công.'; this.passwordForm.reset(); this._cdr.markForCheck(); },
        error: e => { this.passwordErr = this._err(e, 'Đổi mật khẩu thất bại.'); this._cdr.markForCheck(); },
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

  toggleInvoice(id?: string): void { this.expandedId = this.expandedId === id ? null : (id ?? null); }

  cancelBooking(inv: PaymentServiceAgent.InvoiceDTO): void {
    if (!inv.id || !confirm('Bạn có chắc muốn hủy đặt vé này?')) { return; }
    this._payment.cancelBooking(PaymentServiceAgent.CancelBookingRequest.fromJS({ invoiceId: inv.id }))
      .subscribe({ next: () => this.loadInvoices(), error: () => this.loadInvoices() });
  }

  initials(name?: string): string {
    const parts = (name ?? '').trim().split(/\s+/);
    return ((parts[0]?.[0] ?? '') + (parts[parts.length - 1]?.[0] ?? '')).toUpperCase() || 'U';
  }
  statusLabel(s?: PaymentServiceAgent.InvoiceStatus): string {
    switch (s) {
      case this.InvoiceStatus.Paid: return 'Đã Thanh Toán';
      case this.InvoiceStatus.Pending: return 'Chờ Thanh Toán';
      case this.InvoiceStatus.Cancelled: return 'Đã Hủy';
      case this.InvoiceStatus.Failed: return 'Thất Bại';
      default: return '—';
    }
  }
  statusClass(s?: PaymentServiceAgent.InvoiceStatus): string {
    switch (s) {
      case this.InvoiceStatus.Paid: return 'is-paid';
      case this.InvoiceStatus.Pending: return 'is-pending';
      default: return 'is-cancelled';
    }
  }

  private _err(e: any, fallback: string): string {
    const x = e?.error;
    return (typeof x === 'string' && x) ? x : (x?.error || x?.message || fallback);
  }
}

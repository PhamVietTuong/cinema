import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject } from 'rxjs';
import { debounceTime, takeUntil } from 'rxjs/operators';
import { TranslateService } from '@ngx-translate/core';
import { CinemaServiceAgent, PaymentServiceAgent, ToastService, InvoiceStatusValues, invoiceStatusPillClass } from 'CinemaLib';

type Dto = CinemaServiceAgent.InvoiceAdminDTO;

@Component({
  selector: 'app-invoices',
  standalone: false,
  templateUrl: './invoices.component.html',
})
export class InvoicesManagementComponent implements OnInit, OnDestroy {
  items: Dto[] = [];
  totalCount = 0;
  pageIndex = 1;
  pageSize = 10;
  readonly pageSizeOptions = [5, 10, 20, 50];
  filters: Record<string, string> = {};

  showForm = false;
  editingId: string | null = null;
  form: FormGroup;
  private readonly _formDefaults: unknown;

  readonly InvoiceStatus = CinemaServiceAgent.InvoiceStatus;
  // `name` holds an i18n key; the template pipes it through `translate`.
  readonly statuses = InvoiceStatusValues;

  confirmOpen = false;
  private _pendingDeleteId: string | null = null;

  /** Refund-confirmation modal state. */
  refundConfirmOpen = false;
  private _pendingRefundId: string | null = null;

  private readonly _filter$ = new Subject<void>();
  private readonly _destroy$ = new Subject<void>();

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _payment: PaymentServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _cdr: ChangeDetectorRef,
    private _toast: ToastService,
    private _translate: TranslateService,
  ) {
    this.form = this._fb.group({
      status: [CinemaServiceAgent.InvoiceStatus.Pending, Validators.required],
    });
    this._formDefaults = this.form.getRawValue();
  }

  ngOnInit(): void {
    this._filter$.pipe(debounceTime(300), takeUntil(this._destroy$)).subscribe(() => {
      this.pageIndex = 1;
      this.load();
    });
    this.load();
  }

  ngOnDestroy(): void {
    this._destroy$.next();
    this._destroy$.complete();
  }

  load(): void {
    this._svc.getInvoices(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: this.pageIndex, pageSize: this.pageSize, filters: this._activeFilters(),
    })).subscribe({
      next: r => {
        this.items = r.results ?? [];
        this.totalCount = r.totalCount ?? 0;
        this._cdr.markForCheck();
      },
    });
  }

  onFilterChange(): void {
    this._filter$.next();
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  get rangeStart(): number {
    return this.totalCount === 0 ? 0 : (this.pageIndex - 1) * this.pageSize + 1;
  }

  get rangeEnd(): number {
    return Math.min(this.pageIndex * this.pageSize, this.totalCount);
  }

  goToPage(page: number): void {
    const target = Math.min(Math.max(1, page), this.totalPages);
    if (target !== this.pageIndex) {
      this.pageIndex = target;
      this.load();
    }
  }

  prevPage(): void {
    this.goToPage(this.pageIndex - 1);
  }

  nextPage(): void {
    this.goToPage(this.pageIndex + 1);
  }

  changePageSize(size: number): void {
    this.pageSize = +size;
    this.pageIndex = 1;
    this.load();
  }

  edit(item: Dto): void {
    this.editingId = item.id ?? null;
    this.form.reset(this._formDefaults);
    this.form.patchValue({ status: item.status });
    this.showForm = true;
  }

  save(): void {
    if (!this.form.valid || !this.editingId) {
      this.form.markAllAsTouched();
      return;
    }
    this._svc.updateInvoiceStatus(CinemaServiceAgent.UpdateInvoiceStatusRequest.fromJS({ id: this.editingId, status: this.form.value.status }))
      .subscribe({
        next: () => {
          this.load();
          this.cancelEdit();
        },
        error: e => { this._toast.error(this._err(e, this._translate.instant('common.saveFailed'))); },
      });
  }

  cancelEdit(): void {
    this.showForm = false;
    this.editingId = null;
    this.form.reset(this._formDefaults);
  }

  delete(id?: string): void {
    if (!id) {
      return;
    }
    this._pendingDeleteId = id;
    this.confirmOpen = true;
  }

  confirmDelete(): void {
    const id = this._pendingDeleteId;
    this.confirmOpen = false;
    this._pendingDeleteId = null;
    if (id) {
      this._svc.deleteInvoice(id).subscribe({
        next: () => this.load(),
        error: e => { this._toast.error(this._err(e, this._translate.instant('common.deleteFailed'))); },
      });
    }
  }

  statusLabel(s?: CinemaServiceAgent.InvoiceStatus): string {
    return this.statuses.find(x => x.value === s)?.name ?? '—';
  }
  statusClass(s?: CinemaServiceAgent.InvoiceStatus): string {
    return invoiceStatusPillClass(s);
  }

  // Refund is only valid for a Paid invoice; the server re-checks (check-in / showtime).
  refund(id?: string): void {
    if (!id) { return; }
    this._pendingRefundId = id;
    this.refundConfirmOpen = true;
  }

  confirmRefund(): void {
    const id = this._pendingRefundId;
    this.refundConfirmOpen = false;
    this._pendingRefundId = null;
    if (!id) { return; }
    this._payment.refundBooking(PaymentServiceAgent.RefundBookingRequest.fromJS({ invoiceId: id }))
      .subscribe({
        next: () => {
          this._toast.success(this._translate.instant('invoices.refundSuccess'));
          this.load();
        },
        error: e => {
          this._toast.error(this._err(e, this._translate.instant('invoices.refundFailed')));
        },
      });
  }

  private _err(e: any, fallback: string): string {
    const x = e?.error;
    return (typeof x === 'string' && x) ? x : (x?.error || x?.message || fallback);
  }

  private _activeFilters(): Record<string, string> {
    const out: Record<string, string> = {};
    for (const key of Object.keys(this.filters)) {
      const value = (this.filters[key] ?? '').trim();
      if (value) {
        out[key] = value;
      }
    }
    return out;
  }
}

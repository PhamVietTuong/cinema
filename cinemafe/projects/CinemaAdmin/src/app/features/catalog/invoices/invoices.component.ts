import { Component, inject } from '@angular/core';
import { Validators } from '@angular/forms';
import { EMPTY, Observable } from 'rxjs';
import { TranslateService } from '@ngx-translate/core';
import { SharedModule, CinemaServiceAgent, PaymentServiceAgent, ToastService, InvoiceStatusValues, invoiceStatusPillClass } from 'CinemaLib';
import { CatalogCrudBase } from '../catalog-crud.base';
import { ModalComponent } from '../../../shared/modal.component';
import { ConfirmModalComponent } from '../../../shared/confirm-modal.component';

type Dto = CinemaServiceAgent.InvoiceAdminDTO;

@Component({
  selector: 'app-invoices',
  standalone: true,
  imports: [SharedModule, ModalComponent, ConfirmModalComponent],
  templateUrl: './invoices.component.html',
})
export class InvoicesManagementComponent extends CatalogCrudBase<Dto> {
  private _svc = inject(CinemaServiceAgent.HttpService);
  private _payment = inject(PaymentServiceAgent.HttpService);
  private _toast = inject(ToastService);
  private _translate = inject(TranslateService);

  readonly InvoiceStatus = CinemaServiceAgent.InvoiceStatus;

  // `name` holds an i18n key; the template pipes it through `translate`.
  readonly statuses = InvoiceStatusValues;

  /** Refund-confirmation modal state. */
  refundConfirmOpen = false;
  private _pendingRefundId: string | null = null;

  buildForm() {
    return this._fb.group({
      status: [CinemaServiceAgent.InvoiceStatus.Pending, Validators.required],
    });
  }
  fetch(pageIndex: number, pageSize: number, filters: Record<string, string>) {
    return this._svc.getInvoices(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex, pageSize, filters }));
  }
  // Invoices are created by the booking flow — admin only changes status / deletes.
  create(): Observable<unknown> { return EMPTY; }
  update(v: any, id: string) {
    return this._svc.updateInvoiceStatus(CinemaServiceAgent.UpdateInvoiceStatusRequest.fromJS({ id, status: v.status }));
  }
  remove(id: string) { return this._svc.deleteInvoice(id); }

  protected override toFormValue(i: Dto) {
    return { status: i.status };
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
}

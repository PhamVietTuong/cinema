import { Component, inject } from '@angular/core';
import { Validators } from '@angular/forms';
import { EMPTY, Observable } from 'rxjs';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { CatalogCrudBase } from '../catalog-crud.base';

type Dto = CinemaServiceAgent.InvoiceAdminDTO;

@Component({
  selector: 'app-invoices',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './invoices.component.html',
})
export class InvoicesManagementComponent extends CatalogCrudBase<Dto> {
  private _svc = inject(CinemaServiceAgent.HttpService);

  readonly statuses = [
    { v: CinemaServiceAgent.InvoiceStatus.Pending, label: 'Chờ Xử Lý' },
    { v: CinemaServiceAgent.InvoiceStatus.Paid, label: 'Đã Thanh Toán' },
    { v: CinemaServiceAgent.InvoiceStatus.Cancelled, label: 'Đã Hủy' },
    { v: CinemaServiceAgent.InvoiceStatus.Failed, label: 'Thất Bại' },
  ];

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
    return this.statuses.find(x => x.v === s)?.label ?? '—';
  }
  statusClass(s?: CinemaServiceAgent.InvoiceStatus): string {
    switch (s) {
      case CinemaServiceAgent.InvoiceStatus.Paid: return 'ad-pill--success';
      case CinemaServiceAgent.InvoiceStatus.Pending: return 'ad-pill--warn';
      default: return 'ad-pill--danger';
    }
  }
}

import { ChangeDetectorRef, Component } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { Observable } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';
import {
  CinemaServiceAgent,
  PaymentServiceAgent,
  BaseTableComponent, TablePage, TableSearchCriteria,
  DialogService,
  InvoiceStatusValues, invoiceStatusPillClass,
  showLoading, hideLoading, showSuccess, showException,
} from 'CinemaLib';
import { InvoiceStatusDialog } from './invoice-status.dialog';

type Dto = CinemaServiceAgent.InvoiceAdminDTO;

@Component({
  selector: 'app-invoices',
  standalone: false,
  templateUrl: './invoices.component.html',
})
export class InvoicesManagementComponent extends BaseTableComponent {
  readonly InvoiceStatus = CinemaServiceAgent.InvoiceStatus;
  // `name` holds an i18n key; the template pipes it through `translate`.
  readonly statuses = InvoiceStatusValues;

  constructor(
    cd: ChangeDetectorRef,
    fb: FormBuilder,
    router: Router,
    store: Store<any>,
    private _svc: CinemaServiceAgent.HttpService,
    private _payment: PaymentServiceAgent.HttpService,
    private _dialog: MatDialog,
    private _dialogService: DialogService,
  ) {
    super(cd, fb, router, store);
  }

  protected override _createSearchForm(): void {
    this.searchForm = this._formBuilder.group({ status: [''] });
  }

  protected _search(criteria: TableSearchCriteria): Observable<TablePage<Dto>> {
    return this._svc.getInvoices(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: criteria.pageIndex, pageSize: criteria.pageSize, filters: criteria.filters,
    }));
  }

  edit(item: Dto): void {
    this._dialog.open(InvoiceStatusDialog, { width: '480px', data: { invoice: item } })
      .afterClosed().subscribe(saved => { if (saved) { this.triggerSearch(); } });
  }

  delete(id?: string): void {
    if (!id) {
      return;
    }
    this._dialogService.openConfirmDialog({ message: 'common.confirmDelete' })
      .afterClosed().subscribe(confirmed => {
        if (confirmed) {
          this._deleteConfirmed(id);
        }
      });
  }

  private _deleteConfirmed(id: string): void {
    this._store.dispatch(showLoading());
    this._svc.deleteInvoice(id).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({}));
        this.triggerSearch();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }

  // Refund is only valid for a Paid invoice; the server re-checks (check-in / showtime).
  refund(id?: string): void {
    if (!id) {
      return;
    }
    this._dialogService.openConfirmDialog({
      title: 'invoices.refund',
      message: 'invoices.confirmRefund',
      confirmText: 'invoices.refund',
    }).afterClosed().subscribe(confirmed => {
      if (confirmed) {
        this._refundConfirmed(id);
      }
    });
  }

  private _refundConfirmed(id: string): void {
    this._store.dispatch(showLoading());
    this._payment.refundBooking(PaymentServiceAgent.RefundBookingRequest.fromJS({ invoiceId: id })).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({}));
        this.triggerSearch();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }

  statusLabel(s?: CinemaServiceAgent.InvoiceStatus): string {
    return this.statuses.find(x => x.value === s)?.name ?? '—';
  }

  statusClass(s?: CinemaServiceAgent.InvoiceStatus): string {
    return invoiceStatusPillClass(s);
  }
}

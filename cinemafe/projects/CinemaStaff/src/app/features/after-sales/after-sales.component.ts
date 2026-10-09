import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { Observable, Subject } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import {
  DialogService,
  EmptyStateComponent,
  FilterBarComponent,
  FilterBarField,
  RefundTenderValues,
  SharedModule,
  StaffReasonCodeValues,
  CinemaServiceAgent,
  StatusPillComponent,
  hideLoading,
  selectCurrentUser,
  showError,
  showException,
  showLoading,
  showSuccess,
} from 'CinemaLib';
import { SensitiveCallService } from '../../core/sensitive-call.service';
import { TheaterContextService } from '../../core/theater-context.service';
import {
  canExchangeInvoice,
  canReprintInvoice,
  hasSearchCriteria,
  isValidReprintReason,
  needsManagerOverride,
  refundTenderErrors,
} from './after-sales.logic';
import { ExchangeDialogComponent, ExchangeDialogData } from './exchange.dialog';
import { ReprintTicketsDialogComponent } from './reprint-tickets.dialog';

/**
 * After-sales desk: find an invoice by code or phone, then refund it, exchange it or reprint its tickets.
 * Anyone who is not an approver gets the manager PIN prompt before the sensitive call; a 403 about the approval reopens it.
 */
@Component({
  selector: 'staff-after-sales',
  standalone: true,
  imports: [SharedModule, StatusPillComponent, EmptyStateComponent, FilterBarComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './after-sales.component.html',
  styleUrl: './after-sales.component.scss',
})
export class AfterSalesComponent {
  private readonly _boxOffice = inject(CinemaServiceAgent.HttpService);
  private readonly _theater = inject(TheaterContextService);
  private readonly _store = inject(Store);
  private readonly _dialogs = inject(DialogService);
  private readonly _matDialog = inject(MatDialog);
  private readonly _sensitive = inject(SensitiveCallService);
  private readonly _translate = inject(TranslateService);
  private readonly _fb = inject(FormBuilder);
  private readonly _destroyRef = inject(DestroyRef);

  readonly theaterId = this._theater.currentTheaterId;
  readonly tenders = RefundTenderValues;
  readonly cashTender = CinemaServiceAgent.PaymentTender.Cash;

  readonly filterFields: FilterBarField[] = [
    { key: 'code', type: 'text', labelKey: 'afterSales.search.code' },
    { key: 'phone', type: 'text', labelKey: 'afterSales.search.phone' },
  ];
  readonly searchForm = this._fb.group({ code: [''], phone: [''] });
  readonly tenderControl = this._fb.control<CinemaServiceAgent.PaymentTender>(CinemaServiceAgent.PaymentTender.Cash);
  readonly referenceControl = this._fb.control('');

  readonly invoices = signal<CinemaServiceAgent.AfterSalesInvoiceDTO[]>([]);
  readonly selected = signal<CinemaServiceAgent.AfterSalesInvoiceDTO | null>(null);
  readonly loading = signal(false);
  readonly searched = signal(false);
  readonly busy = signal(false);

  private readonly _role = this._store.selectSignal(selectCurrentUser);
  readonly needsPin = computed(() => needsManagerOverride(this._role()?.userTypeName));
  readonly canExchange = computed(() => {
    const invoice = this.selected();
    return !!invoice && canExchangeInvoice(invoice);
  });
  readonly reprintable = computed(() => {
    const invoice = this.selected();
    return !!invoice && canReprintInvoice(invoice);
  });

  private readonly _filterChange$ = new Subject<void>();

  constructor() {
    this._filterChange$.pipe(debounceTime(400), takeUntilDestroyed(this._destroyRef)).subscribe(() => this.search());
  }

  onFilterChange(): void {
    this._filterChange$.next();
  }

  select(invoice: CinemaServiceAgent.AfterSalesInvoiceDTO): void {
    this.selected.set(invoice);
    this.tenderControl.setValue(CinemaServiceAgent.PaymentTender.Cash);
    this.referenceControl.setValue('');
  }

  search(): void {
    const { code, phone } = this.searchForm.value;
    if (!hasSearchCriteria(code, phone)) {
      this.invoices.set([]);
      this.selected.set(null);
      this.searched.set(false);
      return;
    }
    this.loading.set(true);
    this._store.dispatch(showLoading());
    this._boxOffice.findInvoice(CinemaServiceAgent.FindInvoiceRequest.fromJS({
      theaterId: this.theaterId() ?? undefined,
      code: (code ?? '').trim() || undefined,
      phone: (phone ?? '').trim() || undefined,
    })).subscribe({
      next: invoices => {
        this.invoices.set(invoices ?? []);
        this.selected.set(null);
        this.searched.set(true);
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this.loading.set(false);
      this._store.dispatch(hideLoading());
    });
  }

  refund(invoice: CinemaServiceAgent.AfterSalesInvoiceDTO): void {
    const tender = this.tenderControl.value;
    const reference = (this.referenceControl.value ?? '').trim();
    const tenderErrors = refundTenderErrors(tender, reference);
    if (tenderErrors.length) {
      this._store.dispatch(showError({ message: this._translate.instant(tenderErrors[0]) }));
      return;
    }

    this._dialogs.openReasonDialog({
      titleKey: 'afterSales.refund.title',
      hintKey: invoice.showStarted ? 'afterSales.refund.hintAfterStart' : 'afterSales.refund.hint',
      confirmKey: 'afterSales.refund.confirm',
      confirmColor: 'warn',
      codes: { labelKey: 'afterSales.refund.reason', options: StaffReasonCodeValues.map(r => ({ value: r.value, labelKey: r.name })) },
      note: {
        labelKey: 'afterSales.refund.note',
        requiredWhenCode: CinemaServiceAgent.StaffReasonCode.Other,
      },
    }).afterClosed().subscribe(result => {
      if (!result) {
        return;
      }
      this._execute(
        this.needsPin(),
        override => this._boxOffice.staffRefund(CinemaServiceAgent.StaffRefundRequest.fromJS({
          theaterId: this.theaterId() ?? undefined,
          invoiceId: invoice.id,
          reasonCode: result.code,
          note: result.note || undefined,
          refundTender: tender,
          refundReference: reference || undefined,
          override,
        })),
        done => {
          this._store.dispatch(showSuccess({
            message: this._translate.instant(done.outOfBand ? 'afterSales.refund.successOutOfBand' : 'afterSales.refund.success', {
              code: done.invoiceCode,
              amount: done.refundedAmount,
            }),
          }));
          this.search();
        },
      );
    });
  }

  /** Opens the exchange dialog (replacement sale at the counter); refreshes the list once an exchange went through. */
  exchange(invoice: CinemaServiceAgent.AfterSalesInvoiceDTO): void {
    this._matDialog.open<ExchangeDialogComponent, ExchangeDialogData, boolean>(ExchangeDialogComponent, {
      width: '1240px',
      maxWidth: '98vw',
      maxHeight: '96vh',
      autoFocus: false,
      disableClose: true,
      data: { invoice },
    }).afterClosed().subscribe(exchanged => {
      if (exchanged) {
        this.search();
      }
    });
  }

  reprint(invoice: CinemaServiceAgent.AfterSalesInvoiceDTO): void {
    this._dialogs.openReasonDialog({
      titleKey: 'afterSales.reprint.title',
      hintKey: (invoice.usedTicketCount ?? 0) > 0 ? 'afterSales.reprint.hintUsed' : 'afterSales.reprint.hint',
      confirmKey: 'afterSales.reprint.confirm',
      note: { labelKey: 'afterSales.reprint.reason', required: true },
    }).afterClosed().subscribe(result => {
      if (!result) {
        return;
      }
      const reason = result.note ?? '';
      if (!isValidReprintReason(reason)) {
        this._store.dispatch(showError({ message: this._translate.instant('afterSales.reprint.reasonShort') }));
        return;
      }
      // The API asks for an override on reprint only when a ticket was already used; a 403 reopens the prompt anyway.
      this._execute(
        this.needsPin() && (invoice.usedTicketCount ?? 0) > 0,
        override => this._boxOffice.reprint(CinemaServiceAgent.ReprintRequest.fromJS({
          theaterId: this.theaterId() ?? undefined,
          invoiceId: invoice.id,
          reason,
          override,
        })),
        done => {
          this._matDialog.open(ReprintTicketsDialogComponent, {
            width: '560px',
            maxWidth: '95vw',
            data: { result: done },
          });
        },
      );
    });
  }

  /** Runs a sensitive call through the shared manager-override flow (PIN first when `askPin`, otherwise only after a 403 about the approval). */
  private _execute<T>(
    askPin: boolean,
    call: (override?: CinemaServiceAgent.ManagerOverrideDTO) => Observable<T>,
    onDone: (result: T) => void,
  ): void {
    this.busy.set(true);
    this._store.dispatch(showLoading());
    this._sensitive.run(this.theaterId() ?? undefined, askPin, call).subscribe({
      next: onDone,
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this.busy.set(false);
      this._store.dispatch(hideLoading());
    });
  }
}

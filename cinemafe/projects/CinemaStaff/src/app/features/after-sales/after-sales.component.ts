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
  StaffServiceAgent,
  StatusPillComponent,
  hideLoading,
  selectCurrentUser,
  showError,
  showException,
  showLoading,
  showSuccess,
} from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';
import {
  canExchangeInvoice,
  canReprintInvoice,
  hasSearchCriteria,
  isOverrideRejection,
  isValidReprintReason,
  needsManagerOverride,
  refundTenderErrors,
} from './after-sales.logic';
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
  template: `
<div class="ad-page">
  <div class="ad-page-header">
    <div>
      <h1 class="ad-h1">{{ 'afterSales.title' | translate }}</h1>
      <p class="ad-sub">{{ 'afterSales.subtitle' | translate }}</p>
    </div>
  </div>

  @if (!theaterId()) {
    <mat-card class="ad-card--pad-0">
      <cl-empty-state icon="theaters" messageKey="afterSales.pickTheater" hintKey="afterSales.pickTheaterHint" />
    </mat-card>
  } @else {
    <cl-filter-bar titleKey="afterSales.search.title" [form]="searchForm" [fields]="filterFields" (filtersChange)="onFilterChange()" />

    <mat-card class="ad-card--pad-0">
      <div class="ad-table-wrap">
        <table class="ad-table">
          <thead>
            <tr>
              <th>{{ 'afterSales.col.code' | translate }}</th>
              <th>{{ 'afterSales.col.customer' | translate }}</th>
              <th>{{ 'afterSales.col.movie' | translate }}</th>
              <th>{{ 'afterSales.col.showtime' | translate }}</th>
              <th class="num">{{ 'afterSales.col.amount' | translate }}</th>
              <th>{{ 'common.status' | translate }}</th>
              <th>{{ 'afterSales.col.flags' | translate }}</th>
            </tr>
          </thead>
          <tbody>
            @for (invoice of invoices(); track invoice.id) {
              <tr class="row" [class.selected]="invoice.id === selected()?.id" (click)="select(invoice)">
                <td><strong>{{ invoice.code }}</strong></td>
                <td>{{ invoice.customerName }}<br><span class="muted">{{ invoice.customerPhone }}</span></td>
                <td>{{ invoice.movieTitle }}</td>
                <td>{{ invoice.firstShowStart | date: 'HH:mm dd/MM/yyyy' }}</td>
                <td class="num">{{ invoice.finalAmount | currency: 'VND':'symbol':'1.0-0' }}</td>
                <td><cl-status-pill kind="invoice" [value]="invoice.status" /></td>
                <td class="flags">
                  @if (invoice.showStarted) {
                    <span class="ad-pill ad-pill--warn">{{ 'afterSales.flag.showStarted' | translate }}</span>
                  }
                  @if ((invoice.usedTicketCount ?? 0) > 0) {
                    <span class="ad-pill ad-pill--danger">{{ 'afterSales.flag.used' | translate: { used: invoice.usedTicketCount, total: invoice.ticketCount } }}</span>
                  }
                  @if (invoice.hasFood) {
                    <span class="ad-pill ad-pill--neutral">{{ 'afterSales.flag.food' | translate }}</span>
                  }
                  @if (invoice.exchangedFromInvoiceId) {
                    <span class="ad-pill ad-pill--violet">{{ 'afterSales.flag.exchanged' | translate }}</span>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      @if (!invoices().length && !loading()) {
        <cl-empty-state icon="receipt_long"
          [messageKey]="searched() ? 'afterSales.search.empty' : 'afterSales.search.hint'" />
      }
    </mat-card>

    @if (selected(); as invoice) {
      <mat-card class="panel">
        <h2 class="panel-title">{{ 'afterSales.panel.title' | translate: { code: invoice.code } }}</h2>
        @if (needsPin()) {
          <p class="notice"><mat-icon>admin_panel_settings</mat-icon> {{ 'afterSales.panel.pinRequired' | translate }}</p>
        }
        @if (invoice.showStarted) {
          <p class="notice warn"><mat-icon>warning</mat-icon> {{ 'afterSales.panel.showStarted' | translate }}</p>
        }
        @if (!invoice.canRefund) {
          <p class="notice"><mat-icon>info</mat-icon> {{ 'afterSales.panel.cannotRefund' | translate }}</p>
        }

        <div class="refund-fields">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ 'afterSales.refund.tender' | translate }}</mat-label>
            <mat-select [formControl]="tenderControl">
              @for (option of tenders; track option.value) {
                <mat-option [value]="option.value">{{ option.name | translate }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          @if (tenderControl.value !== cashTender) {
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ 'afterSales.refund.reference' | translate }}</mat-label>
              <input matInput maxlength="100" [formControl]="referenceControl" autocomplete="off">
            </mat-form-field>
          }
        </div>

        <div class="actions">
          <button mat-raised-button color="warn" type="button" [disabled]="busy() || !invoice.canRefund" (click)="refund(invoice)">
            <mat-icon>undo</mat-icon> {{ 'afterSales.refund.button' | translate }}
          </button>
          <span [matTooltip]="(canExchange() ? 'afterSales.exchange.disabledTip' : 'afterSales.exchange.notEligibleTip') | translate">
            <!-- TODO(P5-exchange): enable once the POS replacement-sale picker lands (needs CinemaLib seat map + POS cart). -->
            <button mat-stroked-button type="button" disabled>
              <mat-icon>swap_horiz</mat-icon> {{ 'afterSales.exchange.button' | translate }}
            </button>
          </span>
          <button mat-stroked-button type="button" [disabled]="busy() || !reprintable()" (click)="reprint(invoice)">
            <mat-icon>print</mat-icon> {{ 'afterSales.reprint.button' | translate }}
          </button>
        </div>
      </mat-card>
    }
  }
</div>
`,
  styles: [`
    .num { text-align: right; }
    .muted { color: var(--ml-muted); font-size: 12px; }
    .row { cursor: pointer; }
    .row.selected { background: var(--ml-action-soft, rgba(0, 0, 0, 0.04)); }
    .flags { display: flex; gap: 6px; flex-wrap: wrap; }
    .panel { margin-top: 16px; padding: 16px 24px; }
    .panel-title { margin: 0 0 12px; font-size: 16px; }
    .notice { display: flex; align-items: center; gap: 8px; color: var(--ml-muted); margin: 4px 0; }
    .notice.warn { color: var(--ml-warn-ink); }
    .refund-fields { display: flex; gap: 12px; flex-wrap: wrap; margin: 12px 0; }
    .actions { display: flex; gap: 12px; flex-wrap: wrap; }
  `],
})
export class AfterSalesComponent {
  private readonly _boxOffice = inject(StaffServiceAgent.BoxOfficeHttpService);
  private readonly _theater = inject(TheaterContextService);
  private readonly _store = inject(Store);
  private readonly _dialogs = inject(DialogService);
  private readonly _matDialog = inject(MatDialog);
  private readonly _translate = inject(TranslateService);
  private readonly _fb = inject(FormBuilder);
  private readonly _destroyRef = inject(DestroyRef);

  readonly theaterId = this._theater.currentTheaterId;
  readonly tenders = RefundTenderValues;
  readonly cashTender = StaffServiceAgent.PaymentTender.Cash;

  readonly filterFields: FilterBarField[] = [
    { key: 'code', type: 'text', labelKey: 'afterSales.search.code' },
    { key: 'phone', type: 'text', labelKey: 'afterSales.search.phone' },
  ];
  readonly searchForm = this._fb.group({ code: [''], phone: [''] });
  readonly tenderControl = this._fb.control<StaffServiceAgent.PaymentTender>(StaffServiceAgent.PaymentTender.Cash);
  readonly referenceControl = this._fb.control('');

  readonly invoices = signal<StaffServiceAgent.AfterSalesInvoiceDTO[]>([]);
  readonly selected = signal<StaffServiceAgent.AfterSalesInvoiceDTO | null>(null);
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

  select(invoice: StaffServiceAgent.AfterSalesInvoiceDTO): void {
    this.selected.set(invoice);
    this.tenderControl.setValue(StaffServiceAgent.PaymentTender.Cash);
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
    this._boxOffice.findInvoice(StaffServiceAgent.FindInvoiceRequest.fromJS({
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

  refund(invoice: StaffServiceAgent.AfterSalesInvoiceDTO): void {
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
        requiredWhenCode: StaffServiceAgent.StaffReasonCode.Other,
      },
    }).afterClosed().subscribe(result => {
      if (!result) {
        return;
      }
      this._execute(
        this.needsPin(),
        override => this._boxOffice.staffRefund(StaffServiceAgent.StaffRefundRequest.fromJS({
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

  reprint(invoice: StaffServiceAgent.AfterSalesInvoiceDTO): void {
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
        override => this._boxOffice.reprint(StaffServiceAgent.ReprintRequest.fromJS({
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

  /**
   * Runs a sensitive call. `askPin` opens the manager PIN dialog first; otherwise the call goes without an override and a 403
   * about the approval reopens the dialog and retries with the PIN.
   */
  private _execute<T>(
    askPin: boolean,
    call: (override?: StaffServiceAgent.ManagerOverrideDTO) => Observable<T>,
    onDone: (result: T) => void,
  ): void {
    const prompt = () => {
      this._dialogs.openManagerOverrideDialog({ theaterId: this.theaterId() ?? undefined }).afterClosed().subscribe(override => {
        if (override) {
          attempt(override);
        }
      });
    };
    const attempt = (override?: StaffServiceAgent.ManagerOverrideDTO) => {
      this.busy.set(true);
      this._store.dispatch(showLoading());
      call(override).subscribe({
        next: onDone,
        error: error => {
          if (isOverrideRejection(error)) {
            prompt();
          } else {
            this._store.dispatch(showException({ error }));
          }
        },
      }).add(() => {
        this.busy.set(false);
        this._store.dispatch(hideLoading());
      });
    };

    if (askPin) {
      prompt();
    } else {
      attempt();
    }
  }
}

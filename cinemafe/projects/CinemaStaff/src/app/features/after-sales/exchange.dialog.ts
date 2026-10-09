import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TranslateService } from '@ngx-translate/core';
import {
  DialogService, RefundTenderValues, SeatLockSessionService, SharedModule, StaffReasonCodeValues, CinemaServiceAgent, ToastService, apiErrorMessage,
} from 'CinemaLib';
import { CashDrawerService } from '../../core/cash-drawer.service';
import { CounterCartComponent } from '../../core/counter-cart.component';
import { CounterCartService } from '../../core/counter-cart.service';
import { CounterSummaryComponent } from '../../core/counter-summary.component';
import { CounterTenderPanelComponent } from '../../core/counter-tender-panel.component';
import { OverrideFlowService } from '../../core/override-flow.service';
import { TenderEntry, isCash } from '../../core/pos-calc';
import { PosReceiptComponent, SaleReceipt } from '../../core/pos-receipt.component';
import { SensitiveCallService } from '../../core/sensitive-call.service';
import { TheaterContextService } from '../../core/theater-context.service';
import { exchangeDifference, exchangeNeedsDrawer, exchangeSettlement, exchangeSubmitErrors } from './exchange.logic';

export interface ExchangeDialogData {
  invoice: CinemaServiceAgent.AfterSalesInvoiceDTO;
}

/**
 * Exchange of a counter invoice for a new sale. The replacement is picked exactly like at the POS (shared counter cart,
 * seat map and Quote); the tenders settle only the price difference over the old invoice, a cheaper replacement is paid back
 * through the chosen refund tender. Confirming asks for the reason code and the manager approval, like a refund.
 * Closes with `true` once the exchange went through, so the desk can refresh its list.
 */
@Component({
  selector: 'staff-exchange-dialog',
  standalone: true,
  imports: [SharedModule, CounterCartComponent, CounterSummaryComponent, CounterTenderPanelComponent, PosReceiptComponent],
  providers: [SeatLockSessionService, CounterCartService],
  styleUrl: './exchange.dialog.scss',
  templateUrl: './exchange.dialog.html',
})
export class ExchangeDialogComponent {
  readonly data = inject<ExchangeDialogData>(MAT_DIALOG_DATA);
  private readonly _ref = inject(MatDialogRef<ExchangeDialogComponent, boolean>);
  private readonly _box = inject(CinemaServiceAgent.HttpService);
  private readonly _dialogs = inject(DialogService);
  private readonly _sensitive = inject(SensitiveCallService);
  private readonly _override = inject(OverrideFlowService);
  private readonly _translate = inject(TranslateService);
  private readonly _toast = inject(ToastService);
  private readonly _theater = inject(TheaterContextService);
  readonly cart = inject(CounterCartService);
  readonly drawer = inject(CashDrawerService);

  readonly refundTenders = RefundTenderValues;
  readonly refundTender = signal<CinemaServiceAgent.PaymentTender>(CinemaServiceAgent.PaymentTender.Cash);
  readonly tenders = signal<TenderEntry[]>([]);
  readonly submitting = signal(false);
  readonly done = signal<CinemaServiceAgent.ExchangeResultDTO | null>(null);
  readonly receipt = signal<SaleReceipt | null>(null);

  readonly oldAmount = computed(() => this.data.invoice.finalAmount ?? 0);
  readonly diff = computed(() => exchangeDifference(this.oldAmount(), this.cart.due(), this.cart.hasItems()));
  readonly settlement = computed(() => exchangeSettlement(this.diff(), this.tenders()));
  readonly needsDrawer = computed(() => exchangeNeedsDrawer(this.diff(), this.settlement(), this.refundTender()));
  readonly needsPin = computed(() => !this._override.isApprover());
  readonly blockers = computed(() => exchangeSubmitErrors({
    hasQuote: !!this.cart.quote(),
    quoting: this.cart.quoting(),
    submitting: this.submitting(),
    hasItems: this.cart.hasItems(),
    diff: this.diff(),
    settlement: this.settlement(),
    drawerOpen: this.drawer.isOpen(),
    refundTender: this.refundTender(),
  }));

  constructor() {
    effect(() => {
      this.cart.theaterId();
      untracked(() => this.drawer.refresh().subscribe({ error: () => {} }));
    });
    // Nothing to tender once the replacement is not dearer than the old invoice.
    effect(() => {
      if (this.diff().collect === 0 && this.tenders().length > 0) {
        untracked(() => this.tenders.set([]));
      }
    });
  }

  close(): void {
    this._ref.close(this.done() !== null);
  }

  confirm(): void {
    const req = this.cart.quoteRequest();
    if (!req || this.blockers().length > 0) {
      return;
    }
    const st = this.cart.showtime();
    const tickets = this.cart.tickets();
    this._dialogs.openReasonDialog({
      titleKey: 'afterSales.exchange.reasonTitle',
      hintKey: 'afterSales.exchange.reasonHint',
      confirmKey: 'afterSales.exchange.confirm',
      codes: { labelKey: 'afterSales.refund.reason', options: StaffReasonCodeValues.map(r => ({ value: r.value, labelKey: r.name })) },
      note: { labelKey: 'afterSales.refund.note', requiredWhenCode: CinemaServiceAgent.StaffReasonCode.Other },
    }).afterClosed().subscribe(result => {
      if (!result) {
        return;
      }
      const theaterId = this._theater.currentTheaterId() ?? undefined;
      this.submitting.set(true);
      // One approval covers the exchange and any price override on the replacement, so the sale itself carries none.
      this._sensitive.run(theaterId, this.needsPin(), override => this._box.exchange(CinemaServiceAgent.ExchangeRequest.fromJS({
        theaterId,
        invoiceId: this.data.invoice.id,
        reasonCode: result.code,
        note: result.note || undefined,
        refundTender: this.refundTender(),
        override,
        newSale: {
          ...req,
          override: undefined,
          connectionId: this.cart.connectionId,
          tenders: this.diff().collect > 0
            ? this.tenders().map(t => ({
              method: t.method,
              amount: Math.round(t.amount),
              reference: isCash(t.method) ? undefined : t.reference.trim(),
            }))
            : [],
        },
      }))).subscribe({
        next: exchanged => {
          this.submitting.set(false);
          this.done.set(exchanged);
          this.receipt.set({
            result: exchanged.newSale!,
            movieTitle: tickets.length > 0 ? (st?.movieTitle ?? '') : '',
            roomName: tickets.length > 0 ? (st?.roomName ?? '') : '',
            showTime: tickets.length > 0 ? st?.startTime : undefined,
            theaterName: this._theater.currentTheaterName() ?? '',
          });
          this._toast.success(this._translate.instant('afterSales.exchange.success', { code: exchanged.newSale?.invoiceCode }));
          this.cart.afterSale(tickets);
          this.drawer.refresh().subscribe({ error: () => {} });
        },
        error: err => {
          this.submitting.set(false);
          this._toast.error(apiErrorMessage(err, this._translate.instant('afterSales.exchange.failed')));
        },
        complete: () => this.submitting.set(false),
      });
    });
  }
}

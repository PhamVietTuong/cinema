import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TranslateService } from '@ngx-translate/core';
import {
  DialogService, RefundTenderValues, SeatLockSessionService, SharedModule, StaffReasonCodeValues, StaffServiceAgent, ToastService, apiErrorMessage,
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
  invoice: StaffServiceAgent.AfterSalesInvoiceDTO;
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
  template: `
<div mat-dialog-title class="dialog-title">{{ 'afterSales.exchange.title' | translate: { code: data.invoice.code } }}</div>
<mat-dialog-content class="xd-content">
  @if (receipt(); as r) {
    <staff-pos-receipt [receipt]="r" titleKey="afterSales.exchange.done" doneKey="common.close" doneIcon="check" (newSale)="close()">
      <p class="xd-result">
        @if ((done()?.amountCollected ?? 0) > 0) {
          {{ 'afterSales.exchange.collected' | translate }}: <strong>{{ done()?.amountCollected | number:'1.0-0' }}đ</strong>
        } @else if ((done()?.refundedBack ?? 0) > 0) {
          {{ 'afterSales.exchange.paidBack' | translate }}: <strong>{{ done()?.refundedBack | number:'1.0-0' }}đ</strong>
        } @else {
          {{ 'afterSales.exchange.even' | translate }}
        }
      </p>
    </staff-pos-receipt>
  } @else {
    <section class="ad-card xd-old">
      <h2 class="ad-card-title">{{ 'afterSales.exchange.oldInvoice' | translate }}</h2>
      <div class="xd-old-row">
        <strong>{{ data.invoice.code }}</strong>
        <span>{{ data.invoice.movieTitle }}</span>
        <span>{{ data.invoice.firstShowStart | date: 'HH:mm dd/MM/yyyy' }}</span>
        <span>{{ data.invoice.customerName }}</span>
        <strong>{{ data.invoice.finalAmount | number:'1.0-0' }}đ</strong>
      </div>
      <p class="xd-hint">{{ 'afterSales.exchange.hint' | translate }}</p>
    </section>

    <div class="xd-layout">
      <div class="xd-main">
        <staff-counter-cart [busy]="submitting()" [allowFnbOnly]="false" />
      </div>
      <aside class="xd-side">
        <staff-counter-summary />

        <section class="ad-card">
          <h2 class="ad-card-title">{{ 'afterSales.exchange.difference' | translate }}</h2>
          <dl class="xd-diff">
            <div><dt>{{ 'afterSales.exchange.oldAmount' | translate }}</dt><dd>{{ oldAmount() | number:'1.0-0' }}đ</dd></div>
            <div><dt>{{ 'afterSales.exchange.newAmount' | translate }}</dt><dd>{{ cart.due() | number:'1.0-0' }}đ</dd></div>
            @if (diff().collect > 0) {
              <div class="is-collect"><dt>{{ 'afterSales.exchange.collect' | translate }}</dt><dd>{{ diff().collect | number:'1.0-0' }}đ</dd></div>
            } @else if (diff().payBack > 0) {
              <div class="is-payback"><dt>{{ 'afterSales.exchange.payBack' | translate }}</dt><dd>{{ diff().payBack | number:'1.0-0' }}đ</dd></div>
            } @else if (cart.quote()) {
              <div><dt>{{ 'afterSales.exchange.even' | translate }}</dt><dd>0đ</dd></div>
            }
          </dl>
          @if (diff().payBack > 0) {
            <mat-form-field appearance="outline" subscriptSizing="dynamic" class="xd-full">
              <mat-label>{{ 'afterSales.exchange.refundTender' | translate }}</mat-label>
              <mat-select [value]="refundTender()" (selectionChange)="refundTender.set($event.value)">
                @for (option of refundTenders; track option.value) {
                  <mat-option [value]="option.value">{{ option.name | translate }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
          }
        </section>

        @if (diff().collect > 0) {
          <staff-counter-tender-panel titleKey="afterSales.exchange.collectTitle" dueLabelKey="afterSales.exchange.collect"
                                      [due]="diff().collect" [disabled]="!cart.quote()" [(tenders)]="tenders" />
        }
        @if (needsDrawer() && !drawer.isOpen() && diff().collect === 0) {
          <p class="pos-warn">{{ 'pos.pay.needDrawer' | translate }}</p>
        }
        @if (needsPin()) {
          <p class="xd-hint"><mat-icon>admin_panel_settings</mat-icon> {{ 'afterSales.panel.pinRequired' | translate }}</p>
        }
        @if (blockers().length > 0 && cart.hasItems()) {
          <p class="xd-hint">{{ blockers()[0] | translate }}</p>
        }
        <button mat-raised-button color="primary" type="button" class="xd-confirm" [disabled]="blockers().length > 0" (click)="confirm()">
          <mat-icon>swap_horiz</mat-icon> {{ 'afterSales.exchange.confirm' | translate }}
        </button>
      </aside>
    </div>
  }
</mat-dialog-content>
@if (!receipt()) {
  <div mat-dialog-actions class="dialog-actions">
    <button mat-button type="button" (click)="close()">{{ 'common.cancel' | translate }}</button>
  </div>
}
`,
})
export class ExchangeDialogComponent {
  readonly data = inject<ExchangeDialogData>(MAT_DIALOG_DATA);
  private readonly _ref = inject(MatDialogRef<ExchangeDialogComponent, boolean>);
  private readonly _box = inject(StaffServiceAgent.BoxOfficeHttpService);
  private readonly _dialogs = inject(DialogService);
  private readonly _sensitive = inject(SensitiveCallService);
  private readonly _override = inject(OverrideFlowService);
  private readonly _translate = inject(TranslateService);
  private readonly _toast = inject(ToastService);
  private readonly _theater = inject(TheaterContextService);
  readonly cart = inject(CounterCartService);
  readonly drawer = inject(CashDrawerService);

  readonly refundTenders = RefundTenderValues;
  readonly refundTender = signal<StaffServiceAgent.PaymentTender>(StaffServiceAgent.PaymentTender.Cash);
  readonly tenders = signal<TenderEntry[]>([]);
  readonly submitting = signal(false);
  readonly done = signal<StaffServiceAgent.ExchangeResultDTO | null>(null);
  readonly receipt = signal<SaleReceipt | null>(null);

  readonly oldAmount = computed(() => this.data.invoice.finalAmount ?? 0);
  readonly diff = computed(() => exchangeDifference(this.oldAmount(), this.cart.due()));
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
      note: { labelKey: 'afterSales.refund.note', requiredWhenCode: StaffServiceAgent.StaffReasonCode.Other },
    }).afterClosed().subscribe(result => {
      if (!result) {
        return;
      }
      const theaterId = this._theater.currentTheaterId() ?? undefined;
      this.submitting.set(true);
      // One approval covers the exchange and any price override on the replacement, so the sale itself carries none.
      this._sensitive.run(theaterId, this.needsPin(), override => this._box.exchange(StaffServiceAgent.ExchangeRequest.fromJS({
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

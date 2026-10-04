import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { FormControl } from '@angular/forms';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import {
  DialogService,
  EmptyStateComponent,
  SharedModule,
  StaffServiceAgent,
  StatusPillComponent,
  hideLoading,
  paymentTenderLabel,
  showException,
  showLoading,
  showSuccess,
} from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';
import { fromDateInputValue, toDateInputValue, varianceTone } from './cash-close.logic';
import { buildDailyClosePrintHtml } from './daily-close-print';

/**
 * Daily close report for approvers: tenders, refunds / exchanges / comps, tickets and food sold, and every drawer session of the
 * business day with its variance. Closed drawers (variance not yet accepted) can be reconciled right here.
 */
@Component({
  selector: 'staff-daily-close',
  standalone: true,
  imports: [SharedModule, StatusPillComponent, EmptyStateComponent],
  providers: [CurrencyPipe, DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
@if (!theaterId()) {
  <mat-card class="ad-card--pad-0">
    <cl-empty-state icon="theaters" messageKey="cashClose.pickTheater" hintKey="cashClose.pickTheaterHint" />
  </mat-card>
} @else {
  <div class="toolbar">
    <mat-form-field appearance="outline" subscriptSizing="dynamic">
      <mat-label>{{ 'cashClose.daily.date' | translate }}</mat-label>
      <input matInput type="date" [formControl]="dateControl" (change)="load()">
      <mat-hint>{{ 'cashClose.daily.dateHint' | translate }}</mat-hint>
    </mat-form-field>
    <button mat-stroked-button type="button" (click)="load()"><mat-icon>refresh</mat-icon> {{ 'cashClose.daily.refresh' | translate }}</button>
    <button mat-stroked-button type="button" [disabled]="!report()" (click)="print()"><mat-icon>print</mat-icon> {{ 'cashClose.daily.print' | translate }}</button>
  </div>

  @if (report(); as r) {
    @if (pending().length) {
      <mat-card class="panel warn-card">
        <h2 class="panel-title">{{ 'cashClose.daily.pendingTitle' | translate: { count: pending().length } }}</h2>
        <p class="muted">{{ 'cashClose.daily.pendingHint' | translate }}</p>
        <div class="ad-table-wrap">
          <table class="ad-table">
            <tbody>
              @for (d of pending(); track d.sessionId) {
                <tr>
                  <td>{{ d.terminalName }}<br><span class="muted">{{ d.userName }}</span></td>
                  <td class="num">{{ d.variance | currency: 'VND':'symbol':'1.0-0' }}</td>
                  <td class="num">
                    <button mat-raised-button color="primary" type="button" [disabled]="busy()" (click)="reconcile(d)">
                      {{ 'cashClose.daily.reconcile' | translate }}
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </mat-card>
    }

    <div class="grid">
      <mat-card class="panel">
        <h2 class="panel-title">{{ 'cashClose.daily.tenders' | translate }}</h2>
        <table class="ad-table">
          <thead>
            <tr>
              <th>{{ 'cashClose.daily.tender' | translate }}</th>
              <th class="num">{{ 'cashClose.daily.count' | translate }}</th>
              <th class="num">{{ 'cashClose.daily.amount' | translate }}</th>
            </tr>
          </thead>
          <tbody>
            @for (t of r.tenders ?? []; track t.method) {
              <tr>
                <td>{{ tenderName(t.method) | translate }}</td>
                <td class="num">{{ t.count }}</td>
                <td class="num">{{ t.amount | currency: 'VND':'symbol':'1.0-0' }}</td>
              </tr>
            }
            <tr class="total">
              <td colspan="2">{{ 'cashClose.daily.paymentsTotal' | translate }}</td>
              <td class="num">{{ r.paymentsTotal | currency: 'VND':'symbol':'1.0-0' }}</td>
            </tr>
          </tbody>
        </table>
      </mat-card>

      <mat-card class="panel">
        <h2 class="panel-title">{{ 'cashClose.daily.summary' | translate }}</h2>
        <dl class="summary">
          <dt>{{ 'cashClose.daily.moneyCollected' | translate }}</dt><dd>{{ r.moneyCollected | currency: 'VND':'symbol':'1.0-0' }}</dd>
          <dt>{{ 'cashClose.daily.refunds' | translate }}</dt><dd>{{ r.refundCount }} &middot; {{ r.refundAmount | currency: 'VND':'symbol':'1.0-0' }}</dd>
          <dt>{{ 'cashClose.daily.exchanges' | translate }}</dt><dd>{{ r.exchangeCount }}</dd>
          <dt>{{ 'cashClose.daily.comps' | translate }}</dt><dd>{{ r.compCount }} &middot; {{ r.compAmount | currency: 'VND':'symbol':'1.0-0' }}</dd>
          <dt><strong>{{ 'cashClose.daily.netCollected' | translate }}</strong></dt><dd><strong>{{ r.netCollected | currency: 'VND':'symbol':'1.0-0' }}</strong></dd>
          <dt>{{ 'cashClose.daily.tickets' | translate }}</dt><dd>{{ r.ticketsSold }}</dd>
          <dt>{{ 'cashClose.daily.foodItems' | translate }}</dt><dd>{{ r.foodItemsSold }}</dd>
          <dt>{{ 'cashClose.daily.foodRevenue' | translate }}</dt><dd>{{ r.foodRevenue | currency: 'VND':'symbol':'1.0-0' }}</dd>
        </dl>
      </mat-card>
    </div>

    <mat-card class="ad-card--pad-0 drawers">
      <h2 class="panel-title pad">{{ 'cashClose.daily.drawers' | translate }}</h2>
      <div class="ad-table-wrap">
        <table class="ad-table">
          <thead>
            <tr>
              <th>{{ 'cashClose.daily.terminal' | translate }}</th>
              <th>{{ 'cashClose.daily.cashier' | translate }}</th>
              <th>{{ 'common.status' | translate }}</th>
              <th class="num">{{ 'cashClose.drawer.expected' | translate }}</th>
              <th class="num">{{ 'cashClose.drawer.counted' | translate }}</th>
              <th class="num">{{ 'cashClose.drawer.variance' | translate }}</th>
            </tr>
          </thead>
          <tbody>
            @for (d of r.drawers ?? []; track d.sessionId) {
              <tr>
                <td>{{ d.terminalName }}<br><span class="muted">{{ d.openedAt | date: 'HH:mm' }} &ndash; {{ d.closedAt | date: 'HH:mm' }}</span></td>
                <td>{{ d.userName }}</td>
                <td><cl-status-pill kind="drawerStatus" [value]="d.status" /></td>
                <td class="num">{{ d.expectedCash | currency: 'VND':'symbol':'1.0-0' }}</td>
                <td class="num">{{ d.countedCash | currency: 'VND':'symbol':'1.0-0' }}</td>
                <td [class]="'num variance variance--' + toneOf(d.variance)">{{ d.variance | currency: 'VND':'symbol':'1.0-0' }}</td>
              </tr>
            }
            <tr class="total">
              <td colspan="5">{{ 'cashClose.daily.totalVariance' | translate }}</td>
              <td [class]="'num variance variance--' + toneOf(r.totalVariance)">{{ r.totalVariance | currency: 'VND':'symbol':'1.0-0' }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      @if (!(r.drawers ?? []).length) {
        <cl-empty-state icon="point_of_sale" messageKey="cashClose.daily.noDrawers" />
      }
    </mat-card>
  } @else if (!loading()) {
    <mat-card class="ad-card--pad-0"><cl-empty-state icon="summarize" messageKey="cashClose.daily.empty" /></mat-card>
  }
}
`,
  styles: [`
    .toolbar { display: flex; gap: 12px; align-items: flex-start; flex-wrap: wrap; margin-bottom: 16px; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-bottom: 16px; }
    .panel { padding: 16px 24px; }
    .panel-title { margin: 0 0 12px; font-size: 16px; }
    .pad { padding: 16px 24px 0; }
    .warn-card { margin-bottom: 16px; border-left: 4px solid var(--ml-warn-ink); }
    .muted { color: var(--ml-muted); font-size: 12px; }
    .num { text-align: right; }
    .total td { font-weight: 600; }
    .summary { display: grid; grid-template-columns: 1fr auto; gap: 6px 24px; margin: 0; }
    .summary dt { color: var(--ml-muted); }
    .summary dd { margin: 0; text-align: right; }
    .variance--short { color: var(--ml-danger-ink); }
    .variance--over { color: var(--ml-warn-ink); }
  `],
})
export class DailyCloseComponent {
  private readonly _report = inject(StaffServiceAgent.StaffReportHttpService);
  private readonly _boxOffice = inject(StaffServiceAgent.BoxOfficeHttpService);
  private readonly _theater = inject(TheaterContextService);
  private readonly _store = inject(Store);
  private readonly _dialogs = inject(DialogService);
  private readonly _translate = inject(TranslateService);
  private readonly _currency = inject(CurrencyPipe);

  readonly theaterId = this._theater.currentTheaterId;
  /** Empty = the server's current business day; filled with the day the server answered for. */
  readonly dateControl = new FormControl('');

  readonly report = signal<StaffServiceAgent.DailyCloseDTO | null>(null);
  readonly loading = signal(false);
  readonly busy = signal(false);

  /** Closed drawers whose variance still waits for a manager. */
  readonly pending = computed(() => (this.report()?.drawers ?? [])
    .filter(d => d.status === StaffServiceAgent.CashDrawerStatus.Closed));

  constructor() {
    effect(() => {
      if (this.theaterId()) {
        this.load();
      } else {
        this.report.set(null);
      }
    });
  }

  tenderName(tender?: StaffServiceAgent.PaymentTender): string {
    return paymentTenderLabel(tender);
  }

  toneOf(variance: number | undefined): string {
    return varianceTone(variance);
  }

  load(): void {
    this.loading.set(true);
    this._store.dispatch(showLoading());
    this._report.getDailyClose(StaffServiceAgent.DailyCloseRequest.fromJS({
      theaterId: this.theaterId() ?? undefined,
      businessDate: fromDateInputValue(this.dateControl.value),
    })).subscribe({
      next: report => {
        this.report.set(report);
        if (report.businessDate) {
          this.dateControl.setValue(toDateInputValue(new Date(report.businessDate)), { emitEvent: false });
        }
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this.loading.set(false);
      this._store.dispatch(hideLoading());
    });
  }

  /** Approvers reconcile without a PIN (the API audits them); the note records why the variance is accepted. */
  reconcile(drawer: StaffServiceAgent.DailyCloseDrawerDTO): void {
    this._dialogs.openReasonDialog({
      titleKey: 'cashClose.reconcile.title',
      hintKey: 'cashClose.reconcile.hint',
      confirmKey: 'cashClose.reconcile.confirm',
      note: { labelKey: 'cashClose.reconcile.note', required: true },
    }).afterClosed().subscribe(result => {
      if (!result) {
        return;
      }
      this.busy.set(true);
      this._store.dispatch(showLoading());
      this._boxOffice.reconcileDrawer(StaffServiceAgent.ReconcileDrawerRequest.fromJS({
        theaterId: this.theaterId() ?? undefined,
        sessionId: drawer.sessionId,
        note: result.note ?? '',
      })).subscribe({
        next: () => {
          this._store.dispatch(showSuccess({ message: this._translate.instant('cashClose.reconcile.done') }));
          this.load();
        },
        error: error => this._store.dispatch(showException({ error })),
      }).add(() => {
        this.busy.set(false);
        this._store.dispatch(hideLoading());
      });
    });
  }

  print(): void {
    const report = this.report();
    const win = report ? window.open('', '_blank', 'width=720,height=800') : null;
    if (!report || !win) {
      return;
    }
    const t = (key: string) => this._translate.instant(`cashClose.daily.${key}`);
    win.document.write(buildDailyClosePrintHtml(report, {
      title: t('print'),
      heading: `${t('print')} ${this.dateControl.value ?? ''}`,
      tenders: t('tenders'),
      tender: t('tender'),
      amount: t('amount'),
      count: t('count'),
      summary: {
        paymentsTotal: t('paymentsTotal'),
        moneyCollected: t('moneyCollected'),
        refunds: t('refunds'),
        exchanges: t('exchanges'),
        comps: t('comps'),
        netCollected: t('netCollected'),
        tickets: t('tickets'),
        foodItems: t('foodItems'),
        foodRevenue: t('foodRevenue'),
        totalVariance: t('totalVariance'),
      },
      tenderName: tender => this._translate.instant(paymentTenderLabel(tender)),
      money: value => this._currency.transform(value ?? 0, 'VND', 'symbol', '1.0-0') ?? '',
    }));
    win.document.close();
    win.focus();
    win.print();
  }
}

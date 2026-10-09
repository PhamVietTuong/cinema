import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { FormControl } from '@angular/forms';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import {
  DialogService,
  EmptyStateComponent,
  SharedModule,
  CinemaServiceAgent,
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
  templateUrl: './daily-close.component.html',
  styleUrl: './daily-close.component.scss',
})
export class DailyCloseComponent {
  private readonly _report = inject(CinemaServiceAgent.HttpService);
  private readonly _theater = inject(TheaterContextService);
  private readonly _store = inject(Store);
  private readonly _dialogs = inject(DialogService);
  private readonly _translate = inject(TranslateService);
  private readonly _currency = inject(CurrencyPipe);

  readonly theaterId = this._theater.currentTheaterId;
  /** Empty = the server's current business day; filled with the day the server answered for. */
  readonly dateControl = new FormControl('');

  readonly report = signal<CinemaServiceAgent.DailyCloseDTO | null>(null);
  readonly loading = signal(false);
  readonly busy = signal(false);

  /** Closed drawers whose variance still waits for a manager. */
  readonly pending = computed(() => (this.report()?.drawers ?? [])
    .filter(d => d.status === CinemaServiceAgent.CashDrawerStatus.Closed));

  constructor() {
    effect(() => {
      if (this.theaterId()) {
        this.load();
      } else {
        this.report.set(null);
      }
    });
  }

  tenderName(tender?: CinemaServiceAgent.PaymentTender): string {
    return paymentTenderLabel(tender);
  }

  toneOf(variance: number | undefined): string {
    return varianceTone(variance);
  }

  load(): void {
    this.loading.set(true);
    this._store.dispatch(showLoading());
    this._report.getDailyClose(CinemaServiceAgent.DailyCloseRequest.fromJS({
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
  reconcile(drawer: CinemaServiceAgent.DailyCloseDrawerDTO): void {
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
      this._report.reconcileDrawer(CinemaServiceAgent.ReconcileDrawerRequest.fromJS({
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

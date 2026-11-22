import { ChangeDetectionStrategy, ChangeDetectorRef, Component } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { Observable, forkJoin, takeUntil } from 'rxjs';
import { CinemaServiceAgent, PaymentServiceAgent, BaseTableComponent, TablePage, TableSearchCriteria } from 'CinemaLib';
import { TranslateService } from '@ngx-translate/core';
import { environment } from '../../../../../environments/environment';

/** One day of RevenueByDayDTO from GET /api/Payment/GetRevenueByDay. */
interface RevenueDay { date?: string; total?: number; }

type Dto = PaymentServiceAgent.InvoiceDTO;

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-dashboard',
  standalone: false,
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent extends BaseTableComponent {
  override pageSize = 8;

  stats = { movies: 0, theaters: 0, invoicesToday: 0, revenueToday: 0 };
  topMovies: CinemaServiceAgent.MovieDTO[] = [];

  // Revenue trend; bar height is each day's revenue as a % of the period's peak.
  revenueDays = 7;
  revenueTrend: { day: string; value: number }[] = [];

  constructor(
    cd: ChangeDetectorRef,
    fb: FormBuilder,
    router: Router,
    store: Store<any>,
    private _cinema: CinemaServiceAgent.HttpService,
    private _payment: PaymentServiceAgent.HttpService,
    private _http: HttpClient,
    private _translate: TranslateService,
  ) {
    super(cd, fb, router, store);
  }

  protected _search(criteria: TableSearchCriteria): Observable<TablePage<Dto>> {
    return this._payment.getInvoices(PaymentServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: criteria.pageIndex, pageSize: criteria.pageSize, filters: criteria.filters,
    }));
  }

  override ngOnInit(): void {
    super.ngOnInit();

    const count = (r: { totalCount?: number }) => r.totalCount ?? 0;

    // "Orders today" needs its own date-filtered count. Reading totalCount off the list above
    // reported every invoice ever created, and summing that page's Paid rows meant "revenue today"
    // only ever counted whatever happened to fall in the first 8 rows.
    const now = new Date();
    const midnight = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    // One forkJoin: the four tiles and the top-movies list land together in a single render.
    forkJoin({
      movies: this._cinema.getMovies(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 1 })),
      theaters: this._cinema.getTheaters(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 1 })),
      top: this._cinema.getNowShowingMovies(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 5 })),
      today: this._payment.getInvoices(PaymentServiceAgent.PagingSearchDTO.fromJS({
        pageIndex: 1,
        pageSize: 1,
        filters: { from: this._isoLocal(midnight), to: this._isoLocal(now) },
      })),
    }).pipe(takeUntil(this.ngDestroyed$)).subscribe({
      next: r => {
        // Replace, never mutate: the view is OnPush.
        this.stats = { ...this.stats, movies: count(r.movies), theaters: count(r.theaters), invoicesToday: count(r.today) };
        this.topMovies = r.top.results ?? [];
        this._cd.markForCheck();
      },
      error: () => this._cd.markForCheck(),
    });

    this.loadRevenue();
  }

  onPeriodChange(days: number): void {
    this.revenueDays = +days;
    this.loadRevenue();
  }

  private loadRevenue(): void {
    // Direct call; typed PaymentServiceAgent.getRevenueByDay lands after NSwag regen.
    this._http.get<RevenueDay[]>(`${environment.apiUrl}/api/Payment/GetRevenueByDay`, { params: { days: this.revenueDays } })
      .pipe(takeUntil(this.ngDestroyed$))
      .subscribe({
        next: days => {
          const series = days ?? [];
          this.revenueTrend = this._toTrend(series);
          // The series always ends on today, so today's revenue comes from the server's own
          // Paid-invoice totals rather than being re-derived from a page of the invoice list.
          this.stats = { ...this.stats, revenueToday: series.length ? (series[series.length - 1].total ?? 0) : 0 };
          this._cd.markForCheck();
        },
        error: () => { this.revenueTrend = []; this._cd.markForCheck(); },
      });
  }

  /** Local wall-clock ISO with no timezone suffix — the server's date filters compare against
   *  values written the same way, so sending a UTC instant here would shift the day boundary. */
  private _isoLocal(d: Date): string {
    const p = (n: number) => `${n}`.padStart(2, '0');
    return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
  }

  private _toTrend(days: RevenueDay[]): { day: string; value: number }[] {
    const labels = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];
    const max = Math.max(1, ...days.map(d => d.total ?? 0));
    return days.map(d => {
      const dt = d.date ? new Date(d.date) : null;
      return {
        day: dt ? (this.revenueDays > 7 ? `${dt.getDate()}` : labels[dt.getDay()]) : '',
        value: Math.round((d.total ?? 0) / max * 100),
      };
    });
  }

  statusLabel(s?: PaymentServiceAgent.InvoiceStatus): string {
    switch (s) {
      case PaymentServiceAgent.InvoiceStatus.Paid: return this._translate.instant('dashboard.statusPaid');
      case PaymentServiceAgent.InvoiceStatus.Pending: return this._translate.instant('dashboard.statusPending');
      case PaymentServiceAgent.InvoiceStatus.Cancelled: return this._translate.instant('dashboard.statusCancelled');
      case PaymentServiceAgent.InvoiceStatus.Failed: return this._translate.instant('dashboard.statusFailed');
      default: return '—';
    }
  }

  statusClass(s?: PaymentServiceAgent.InvoiceStatus): string {
    switch (s) {
      case PaymentServiceAgent.InvoiceStatus.Paid: return 'ad-pill--success';
      case PaymentServiceAgent.InvoiceStatus.Pending: return 'ad-pill--warn';
      case PaymentServiceAgent.InvoiceStatus.Cancelled:
      case PaymentServiceAgent.InvoiceStatus.Failed: return 'ad-pill--danger';
      default: return 'ad-pill--neutral';
    }
  }
}

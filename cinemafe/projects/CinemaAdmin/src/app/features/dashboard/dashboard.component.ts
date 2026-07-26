import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { SharedModule, CinemaServiceAgent, PaymentServiceAgent } from 'CinemaLib';
import { TranslateService } from '@ngx-translate/core';
import { environment } from '../../../environments/environment';

/** One day of RevenueByDayDTO from GET /api/Payment/GetRevenueByDay. */
interface RevenueDay { date?: string; total?: number; }

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  stats = { movies: 0, theaters: 0, invoicesToday: 0, revenueToday: 0 };
  invoices: PaymentServiceAgent.InvoiceDTO[] = [];
  topMovies: CinemaServiceAgent.MovieDTO[] = [];

  // Revenue trend; bar height is each day's revenue as a % of the period's peak.
  revenueDays = 7;
  revenueTrend: { day: string; value: number }[] = [];

  constructor(
    private _cinema: CinemaServiceAgent.HttpService,
    private _payment: PaymentServiceAgent.HttpService,
    private _http: HttpClient,
    private _cdr: ChangeDetectorRef,
    private _translate: TranslateService,
  ) {}

  ngOnInit(): void {
    this._cinema.getMovies(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 1 }))
      .subscribe(r => { this.stats.movies = r.totalCount ?? 0; this._cdr.markForCheck(); });

    this._cinema.getTheaters(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 1 }))
      .subscribe(r => { this.stats.theaters = r.totalCount ?? 0; this._cdr.markForCheck(); });

    this._cinema.getNowShowingMovies(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 5 }))
      .subscribe(r => { this.topMovies = r.results ?? []; this._cdr.markForCheck(); });

    // Recent orders list — most recent regardless of date.
    this._payment.getInvoices(PaymentServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 8 }))
      .subscribe(r => {
        this.invoices = r.results ?? [];
        this._cdr.markForCheck();
      });

    // "Orders today" needs its own date-filtered count. Reading totalCount off the list above
    // reported every invoice ever created, and summing that page's Paid rows meant "revenue today"
    // only ever counted whatever happened to fall in the first 8 rows.
    const now = new Date();
    const midnight = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    this._payment.getInvoices(PaymentServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: 1,
      pageSize: 1,
      filters: { from: this._isoLocal(midnight), to: this._isoLocal(now) },
    }))
      .subscribe(r => { this.stats.invoicesToday = r.totalCount ?? 0; this._cdr.markForCheck(); });

    this.loadRevenue();
  }

  onPeriodChange(days: number): void {
    this.revenueDays = +days;
    this.loadRevenue();
  }

  private loadRevenue(): void {
    // Direct call; typed PaymentServiceAgent.getRevenueByDay lands after NSwag regen.
    this._http.get<RevenueDay[]>(`${environment.apiUrl}/api/Payment/GetRevenueByDay`, { params: { days: this.revenueDays } })
      .subscribe({
        next: days => {
          const series = days ?? [];
          this.revenueTrend = this._toTrend(series);
          // The series always ends on today, so today's revenue comes from the server's own
          // Paid-invoice totals rather than being re-derived from a page of the invoice list.
          this.stats.revenueToday = series.length ? (series[series.length - 1].total ?? 0) : 0;
          this._cdr.markForCheck();
        },
        error: () => { this.revenueTrend = []; this._cdr.markForCheck(); },
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

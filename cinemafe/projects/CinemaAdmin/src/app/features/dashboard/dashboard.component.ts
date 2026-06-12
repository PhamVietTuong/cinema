import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { SharedModule, CinemaServiceAgent, PaymentServiceAgent } from 'CinemaLib';
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
  ) {}

  ngOnInit(): void {
    this._cinema.getMovies(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 1 }))
      .subscribe(r => { this.stats.movies = r.totalCount ?? 0; this._cdr.markForCheck(); });

    this._cinema.getTheaters(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 1 }))
      .subscribe(r => { this.stats.theaters = r.totalCount ?? 0; this._cdr.markForCheck(); });

    this._cinema.getNowShowingMovies(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 5 }))
      .subscribe(r => { this.topMovies = r.results ?? []; this._cdr.markForCheck(); });

    this._payment.getInvoices(PaymentServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 8 }))
      .subscribe(r => {
        this.invoices = r.results ?? [];
        this.stats.invoicesToday = r.totalCount ?? this.invoices.length;
        this.stats.revenueToday = this.invoices
          .filter(i => i.status === PaymentServiceAgent.InvoiceStatus.Paid)
          .reduce((sum, i) => sum + (i.finalAmount ?? 0), 0);
        this._cdr.markForCheck();
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
      .subscribe({
        next: days => { this.revenueTrend = this._toTrend(days ?? []); this._cdr.markForCheck(); },
        error: () => { this.revenueTrend = []; this._cdr.markForCheck(); },
      });
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
      case PaymentServiceAgent.InvoiceStatus.Paid: return 'Đã Thanh Toán';
      case PaymentServiceAgent.InvoiceStatus.Pending: return 'Chờ Xử Lý';
      case PaymentServiceAgent.InvoiceStatus.Cancelled: return 'Đã Hủy';
      case PaymentServiceAgent.InvoiceStatus.Failed: return 'Thất Bại';
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

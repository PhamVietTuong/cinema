import { Component, OnInit } from '@angular/core';
import { SharedModule, CinemaServiceAgent, PaymentServiceAgent } from 'CinemaLib';

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

  // Illustrative 7-day revenue trend (no analytics endpoint yet).
  revenueTrend = [
    { day: 'T2', value: 62 }, { day: 'T3', value: 48 }, { day: 'T4', value: 75 },
    { day: 'T5', value: 56 }, { day: 'T6', value: 88 }, { day: 'T7', value: 100 },
    { day: 'CN', value: 81 },
  ];

  constructor(
    private _cinema: CinemaServiceAgent.HttpService,
    private _payment: PaymentServiceAgent.HttpService,
  ) {}

  ngOnInit(): void {
    this._cinema.getMovies(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 1 }))
      .subscribe(r => this.stats.movies = r.totalCount ?? 0);

    this._cinema.getTheaters(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 1 }))
      .subscribe(r => this.stats.theaters = r.totalCount ?? 0);

    this._cinema.getNowShowingMovies(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 5 }))
      .subscribe(r => this.topMovies = r.results ?? []);

    this._payment.getInvoices(PaymentServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 8 }))
      .subscribe(r => {
        this.invoices = r.results ?? [];
        this.stats.invoicesToday = r.totalCount ?? this.invoices.length;
        this.stats.revenueToday = this.invoices
          .filter(i => i.status === PaymentServiceAgent.InvoiceStatus.Paid)
          .reduce((sum, i) => sum + (i.finalAmount ?? 0), 0);
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

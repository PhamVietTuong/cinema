import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { SharedModule, PaymentServiceAgent } from 'CinemaLib';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './reports.component.html',
  styleUrl: './reports.component.scss',
})
export class ReportsComponent implements OnInit {
  private _payment = inject(PaymentServiceAgent.HttpService);
  private _cdr = inject(ChangeDetectorRef);

  from = '';
  to = '';
  loading = false;

  total = 0;
  byDay: PaymentServiceAgent.RevenueByDayDTO[] = [];
  byMovie: PaymentServiceAgent.RevenueBreakdownDTO[] = [];
  byTheater: PaymentServiceAgent.RevenueBreakdownDTO[] = [];

  ngOnInit(): void {
    const today = new Date();
    const start = new Date();
    start.setDate(today.getDate() - 29);
    this.to = this._iso(today);
    this.from = this._iso(start);
    this.load();
  }

  load(): void {
    const from = new Date(this.from + 'T00:00:00');
    const to = new Date(this.to + 'T23:59:59');
    const days = Math.max(1, Math.round((to.getTime() - from.getTime()) / 86400000) + 1);
    this.loading = true;

    this._payment.getRevenue(from, to).subscribe({
      next: (r: any) => { this.total = r?.revenue ?? 0; this._cdr.markForCheck(); },
      error: () => this._cdr.markForCheck(),
    });
    this._payment.getRevenueByDay(days).subscribe(r => { this.byDay = r ?? []; this._cdr.markForCheck(); });
    this._payment.getRevenueByMovie(from, to).subscribe(r => { this.byMovie = r ?? []; this._cdr.markForCheck(); });
    this._payment.getRevenueByTheater(from, to).subscribe(r => {
      this.byTheater = r ?? []; this.loading = false; this._cdr.markForCheck();
    });
  }

  get maxDay(): number { return Math.max(1, ...this.byDay.map(d => d.total ?? 0)); }
  get maxMovie(): number { return Math.max(1, ...this.byMovie.map(d => d.total ?? 0)); }
  get maxTheater(): number { return Math.max(1, ...this.byTheater.map(d => d.total ?? 0)); }
  get ticketsTotal(): number { return this.byMovie.reduce((s, m) => s + (m.total ?? 0), 0); }

  pct(value: number | undefined, max: number): number {
    return Math.round(((value ?? 0) / max) * 100);
  }

  private _iso(d: Date): string {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }
}

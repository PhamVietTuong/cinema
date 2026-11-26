import { ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, catchError, finalize, forkJoin, of, switchMap } from 'rxjs';
import { PaymentServiceAgent } from 'CinemaLib';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-reports',
  standalone: false,
  templateUrl: './reports.component.html',
  styleUrl: './reports.component.scss',
})
export class ReportsComponent implements OnInit {
  private _payment = inject(PaymentServiceAgent.HttpService);
  private _cdr = inject(ChangeDetectorRef);
  private _destroyRef = inject(DestroyRef);
  private _load$ = new Subject<{ from: Date; to: Date; days: number }>();

  from = '';
  to = '';
  loading = false;

  total = 0;
  byDay: PaymentServiceAgent.RevenueByDayDTO[] = [];
  byMovie: PaymentServiceAgent.RevenueBreakdownDTO[] = [];
  byTheater: PaymentServiceAgent.RevenueBreakdownDTO[] = [];

  // Derived once per load (not recomputed by template getters on every change-detection pass).
  maxDay = 1;
  maxMovie = 1;
  maxTheater = 1;
  ticketsTotal = 0;

  ngOnInit(): void {
    // A newer load cancels an older in-flight one; the five requests land together in one render.
    this._load$.pipe(
      switchMap(({ from, to, days }) => {
        this.loading = true;
        this._cdr.markForCheck();
        return forkJoin({
          revenue: this._payment.getRevenue(from, to).pipe(catchError(() => of(null))),
          byDay: this._payment.getRevenueByDay(days).pipe(catchError(() => of(null))),
          byMovie: this._payment.getRevenueByMovie(from, to).pipe(catchError(() => of(null))),
          byTheater: this._payment.getRevenueByTheater(from, to).pipe(catchError(() => of(null))),
        }).pipe(finalize(() => { this.loading = false; this._cdr.markForCheck(); }));
      }),
      takeUntilDestroyed(this._destroyRef),
    ).subscribe(r => {
      this.total = (r.revenue as any)?.revenue ?? 0;
      this.byDay = r.byDay ?? [];
      this.byMovie = r.byMovie ?? [];
      this.byTheater = r.byTheater ?? [];
      this.maxDay = Math.max(1, ...this.byDay.map(d => d.total ?? 0));
      this.maxMovie = Math.max(1, ...this.byMovie.map(d => d.total ?? 0));
      this.maxTheater = Math.max(1, ...this.byTheater.map(d => d.total ?? 0));
      this.ticketsTotal = this.byMovie.reduce((s, m) => s + (m.total ?? 0), 0);
      this._cdr.markForCheck();
    });

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
    this._load$.next({ from, to, days });
  }

  pct(value: number | undefined, max: number): number {
    return Math.round(((value ?? 0) / max) * 100);
  }

  exportByDay(): void {
    this._downloadCsv('revenue-by-day', ['Date', 'Revenue (VND)'],
      this.byDay.map(d => [d.date ? this._iso(new Date(d.date)) : '', String(d.total ?? 0)]));
  }
  exportByMovie(): void {
    this._downloadCsv('revenue-by-movie', ['Movie', 'Revenue (VND)'],
      this.byMovie.map(m => [m.name ?? '', String(m.total ?? 0)]));
  }
  exportByTheater(): void {
    this._downloadCsv('revenue-by-theater', ['Theater', 'Revenue (VND)'],
      this.byTheater.map(t => [t.name ?? '', String(t.total ?? 0)]));
  }

  /** Builds a CSV (RFC-4180 quoting, UTF-8 BOM for Excel) and triggers a download. */
  private _downloadCsv(name: string, headers: string[], rows: string[][]): void {
    const esc = (v: string) => `"${v.replace(/"/g, '""')}"`;
    const lines = [headers, ...rows].map(r => r.map(esc).join(','));
    const csv = '﻿' + lines.join('\r\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${name}-${this.from}_${this.to}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  private _iso(d: Date): string {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }
}

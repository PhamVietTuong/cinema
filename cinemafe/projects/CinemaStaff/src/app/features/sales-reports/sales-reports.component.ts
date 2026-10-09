import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { DecimalPipe, PercentPipe } from '@angular/common';
import { Store } from '@ngrx/store';
import { forkJoin } from 'rxjs';
import {
  BarChartComponent,
  CinemaServiceAgent,
  EmptyStateComponent,
  FilterBarComponent,
  FilterBarField,
  FilterBarOption,
  SalesGroupByValues,
  SharedModule,
  UserRoles,
  hideLoading,
  selectCurrentUser,
  showException,
  showLoading,
} from 'CinemaLib';
import {
  RangeError,
  buildReportRequest,
  localDayString,
  occupancyChartRows,
  salesChartRows,
  validateRange,
} from './sales-reports.state';

/** Most occupancy rows listed; a 92-day range can hold thousands of showtimes. */
const MAX_OCCUPANCY_ROWS = 100;

/**
 * Management sales reports: KPI tiles, sales by day/movie/theater/payment/staff/channel and showtime occupancy.
 * An Admin or RegionalManager may narrow to some theaters (none picked = all they may see); a TheaterManager is pinned.
 */
@Component({
  selector: 'staff-sales-reports',
  standalone: true,
  imports: [SharedModule, FilterBarComponent, BarChartComponent, EmptyStateComponent, DecimalPipe, PercentPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
<div class="ad-page">
  <div class="ad-page-header">
    <div>
      <h1 class="ad-h1">{{ 'salesReports.title' | translate }}</h1>
      <p class="ad-sub">{{ 'salesReports.subtitle' | translate }}</p>
    </div>
  </div>

  <cl-filter-bar [form]="form" [fields]="fields()" titleKey="salesReports.filters" (filtersChange)="validate()" />
  <div class="run-row">
    <button mat-raised-button color="primary" type="button" (click)="run()" [disabled]="!!rangeError()">
      <mat-icon>bar_chart</mat-icon> {{ 'salesReports.run' | translate }}
    </button>
    @if (rangeError(); as err) {
      <span class="error">{{ ('salesReports.error.' + err) | translate }}</span>
    }
  </div>

  @if (!loaded()) {
    <mat-card class="ad-card--pad-0"><cl-empty-state icon="insights" messageKey="salesReports.notRun" /></mat-card>
  } @else {
    @if (kpis(); as k) {
      <div class="kpis">
        <div class="ad-card kpi"><span class="kpi-label">{{ 'salesReports.kpi.paidInvoices' | translate }}</span><span class="kpi-val">{{ k.paidInvoices | number: '1.0-0' }}</span></div>
        <div class="ad-card kpi"><span class="kpi-label">{{ 'salesReports.kpi.ticketsSold' | translate }}</span><span class="kpi-val">{{ k.ticketsSold | number: '1.0-0' }}</span></div>
        <div class="ad-card kpi"><span class="kpi-label">{{ 'salesReports.kpi.attachRate' | translate }}</span><span class="kpi-val">{{ k.attachRate | percent: '1.0-1' }}</span></div>
        <div class="ad-card kpi"><span class="kpi-label">{{ 'salesReports.kpi.averageSpend' | translate }}</span><span class="kpi-val">{{ k.averageSpendPerHead | number: '1.0-0' }}đ</span></div>
        <div class="ad-card kpi"><span class="kpi-label">{{ 'salesReports.kpi.refunds' | translate }}</span><span class="kpi-val">{{ k.refundCount | number: '1.0-0' }} &middot; {{ k.refundAmount | number: '1.0-0' }}đ</span></div>
        <div class="ad-card kpi"><span class="kpi-label">{{ 'salesReports.kpi.refundRate' | translate }}</span><span class="kpi-val">{{ k.refundRateByCount | percent: '1.0-1' }} / {{ k.refundRateByAmount | percent: '1.0-1' }}</span></div>
      </div>
    }

    <mat-card class="section">
      <h3 class="ad-card-title">{{ 'salesReports.salesTitle' | translate }}</h3>
      @if (sales()?.rows?.length) {
        <cl-bar-chart [rows]="salesBars()" />
        <div class="table-wrap">
          <table class="report-table">
            <thead>
              <tr>
                <th>{{ 'salesReports.col.group' | translate }}</th>
                <th class="num">{{ 'salesReports.col.invoices' | translate }}</th>
                <th class="num">{{ 'salesReports.col.tickets' | translate }}</th>
                <th class="num">{{ 'salesReports.col.food' | translate }}</th>
                <th class="num">{{ 'salesReports.col.discount' | translate }}</th>
                <th class="num">{{ 'salesReports.col.net' | translate }}</th>
                <th class="num">{{ 'salesReports.col.refunds' | translate }}</th>
              </tr>
            </thead>
            <tbody>
              @for (row of sales()!.rows; track row.key) {
                <tr>
                  <td>{{ row.label }}</td>
                  <td class="num">{{ row.invoiceCount | number: '1.0-0' }}</td>
                  <td class="num">{{ row.ticketRevenue | number: '1.0-0' }}</td>
                  <td class="num">{{ row.foodRevenue | number: '1.0-0' }}</td>
                  <td class="num">{{ row.discountAmount | number: '1.0-0' }}</td>
                  <td class="num">{{ row.netRevenue | number: '1.0-0' }}</td>
                  <td class="num">{{ row.refundCount }} &middot; {{ row.refundAmount | number: '1.0-0' }}</td>
                </tr>
              }
            </tbody>
            @if (sales()!.totals; as t) {
              <tfoot>
                <tr>
                  <th>{{ 'salesReports.col.total' | translate }}</th>
                  <th class="num">{{ t.invoiceCount | number: '1.0-0' }}</th>
                  <th class="num">{{ t.ticketRevenue | number: '1.0-0' }}</th>
                  <th class="num">{{ t.foodRevenue | number: '1.0-0' }}</th>
                  <th class="num">{{ t.discountAmount | number: '1.0-0' }}</th>
                  <th class="num">{{ t.netRevenue | number: '1.0-0' }}</th>
                  <th class="num">{{ t.refundCount }} &middot; {{ t.refundAmount | number: '1.0-0' }}</th>
                </tr>
              </tfoot>
            }
          </table>
        </div>
      } @else {
        <cl-empty-state icon="bar_chart" messageKey="salesReports.noData" />
      }
    </mat-card>

    <mat-card class="section">
      <h3 class="ad-card-title">{{ 'salesReports.occupancyTitle' | translate }}</h3>
      @if (occupancy(); as occ) {
        @if (occ.rows?.length) {
          <p class="muted">
            {{ 'salesReports.occupancySummary' | translate: { sold: occ.totalSoldSeats, active: occ.totalActiveSeats } }}
            &middot; <strong>{{ occ.occupancyRate | percent: '1.0-1' }}</strong>
          </p>
          <div class="table-wrap">
            <table class="report-table">
              <thead>
                <tr>
                  <th>{{ 'salesReports.col.theater' | translate }}</th>
                  <th>{{ 'salesReports.col.room' | translate }}</th>
                  <th>{{ 'salesReports.col.movie' | translate }}</th>
                  <th>{{ 'salesReports.col.showtime' | translate }}</th>
                  <th class="num">{{ 'salesReports.col.seats' | translate }}</th>
                  <th class="rate">{{ 'salesReports.col.rate' | translate }}</th>
                </tr>
              </thead>
              <tbody>
                @for (row of occupancyRows(); track row.showTimeId) {
                  <tr>
                    <td>{{ row.theaterName }}</td>
                    <td>{{ row.roomName }}</td>
                    <td>{{ row.movieTitle }}</td>
                    <td>{{ row.startTime | date: 'dd/MM HH:mm' }}</td>
                    <td class="num">{{ row.soldSeats }} / {{ row.activeSeats }}</td>
                    <td class="rate"><cl-bar-chart [rows]="rateBar(row)" [max]="1" [compact]="true" /></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          @if (occ.rows!.length > occupancyRows().length) {
            <p class="muted">{{ 'salesReports.occupancyTruncated' | translate: { shown: occupancyRows().length, total: occ.rows!.length } }}</p>
          }
        } @else {
          <cl-empty-state icon="event_seat" messageKey="salesReports.noData" />
        }
      }
    </mat-card>
  }
</div>
`,
  styles: [`
    .run-row { display: flex; align-items: center; gap: 16px; margin: 12px 0 16px; }
    .error { color: var(--ml-danger-ink, #b3261e); }
    .muted { color: var(--ml-muted); }
    .kpis { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 12px; margin-bottom: 16px; }
    .kpi { display: flex; flex-direction: column; gap: 4px; padding: 12px 16px; margin: 0; }
    .kpi-label { color: var(--ml-muted); font-size: 12px; }
    .kpi-val { font-size: 20px; font-weight: 600; font-variant-numeric: tabular-nums; }
    .section { padding: 16px; margin-bottom: 16px; }
    .table-wrap { overflow-x: auto; margin-top: 16px; }
    .report-table { width: 100%; border-collapse: collapse; }
    .report-table th, .report-table td { padding: 8px 12px; text-align: left; border-bottom: 1px solid var(--ml-panel-3, rgba(0, 0, 0, 0.08)); }
    .report-table .num { text-align: right; font-variant-numeric: tabular-nums; }
    .report-table .rate { min-width: 160px; }
    .report-table tfoot th { border-top: 2px solid var(--ml-panel-3, rgba(0, 0, 0, 0.2)); }
  `],
})
export class SalesReportsComponent {
  private readonly _fb = inject(FormBuilder);
  private readonly _store = inject(Store);
  private readonly _cinema = inject(CinemaServiceAgent.HttpService);

  private readonly _user = this._store.selectSignal(selectCurrentUser);
  /** A TheaterManager is pinned to their own theater and gets no theater picker. */
  readonly pinnedTheaterId = computed(() =>
    this._user()?.userTypeName === UserRoles.TheaterManager ? (this._user()?.theaterId ?? null) : null);

  readonly form = this._fb.group({
    from: [localDayString(new Date(), 6)],
    to: [localDayString(new Date())],
    groupBy: ['' + CinemaServiceAgent.SalesGroupBy.Day],
    theaterIds: [[] as string[]],
  });

  private readonly _theaterOptions = signal<FilterBarOption[]>([]);
  readonly fields = computed<FilterBarField[]>(() => {
    const fields: FilterBarField[] = [
      { key: 'from', type: 'date', labelKey: 'salesReports.from' },
      { key: 'to', type: 'date', labelKey: 'salesReports.to' },
      {
        key: 'groupBy', type: 'select', labelKey: 'salesReports.groupByLabel', allowEmpty: false,
        options: SalesGroupByValues.map(v => ({ value: '' + v.value, labelKey: v.name })),
      },
    ];
    if (!this.pinnedTheaterId()) {
      fields.push({ key: 'theaterIds', type: 'multiselect', labelKey: 'salesReports.theaters', options: this._theaterOptions() });
    }
    return fields;
  });

  readonly rangeError = signal<RangeError | null>(null);
  readonly loaded = signal(false);
  readonly sales = signal<CinemaServiceAgent.SalesReportDTO | null>(null);
  readonly occupancy = signal<CinemaServiceAgent.OccupancyReportDTO | null>(null);
  readonly kpis = signal<CinemaServiceAgent.StaffKpisDTO | null>(null);

  readonly salesBars = computed(() => salesChartRows(this.sales()?.rows ?? [], value => `${Math.round(value).toLocaleString('vi-VN')}đ`));
  readonly occupancyRows = computed(() => (this.occupancy()?.rows ?? []).slice(0, MAX_OCCUPANCY_ROWS));

  constructor() {
    if (this.pinnedTheaterId()) {
      return;
    }
    this._cinema.getTheaters(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200, filters: {} })).subscribe({
      next: result => this._theaterOptions.set((result.results ?? []).map(t => ({ value: t.id ?? '', label: t.name ?? '' }))),
      error: error => this._store.dispatch(showException({ error })),
    });
  }

  validate(): void {
    const value = this.form.value;
    this.rangeError.set(validateRange(value.from, value.to));
  }

  rateBar(row: CinemaServiceAgent.OccupancyRowDTO) {
    return occupancyChartRows([row], rate => `${Math.round(rate * 100)}%`).map(bar => ({ ...bar, label: '' }));
  }

  run(): void {
    this.validate();
    if (this.rangeError()) {
      return;
    }
    const value = this.form.value;
    const pinned = this.pinnedTheaterId();
    const request = buildReportRequest(
      value.from ?? '',
      value.to ?? '',
      Number(value.groupBy) as CinemaServiceAgent.SalesGroupBy,
      pinned ? [pinned] : (value.theaterIds ?? []),
    );
    this._store.dispatch(showLoading());
    forkJoin({
      sales: this._cinema.getSales(request),
      occupancy: this._cinema.getOccupancy(request),
      kpis: this._cinema.getKpis(request),
    }).subscribe({
      next: result => {
        this.sales.set(result.sales);
        this.occupancy.set(result.occupancy);
        this.kpis.set(result.kpis);
        this.loaded.set(true);
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }
}

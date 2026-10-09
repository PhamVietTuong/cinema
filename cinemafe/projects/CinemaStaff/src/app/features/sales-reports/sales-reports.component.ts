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
  hideLoading,
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
 * An Admin may narrow to some theaters (none picked = all theaters).
 */
@Component({
  selector: 'staff-sales-reports',
  standalone: true,
  imports: [SharedModule, FilterBarComponent, BarChartComponent, EmptyStateComponent, DecimalPipe, PercentPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './sales-reports.component.html',
  styleUrl: './sales-reports.component.scss',
})
export class SalesReportsComponent {
  private readonly _fb = inject(FormBuilder);
  private readonly _store = inject(Store);
  private readonly _cinema = inject(CinemaServiceAgent.HttpService);

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
    fields.push({ key: 'theaterIds', type: 'multiselect', labelKey: 'salesReports.theaters', options: this._theaterOptions() });
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
    const request = buildReportRequest(
      value.from ?? '',
      value.to ?? '',
      Number(value.groupBy) as CinemaServiceAgent.SalesGroupBy,
      value.theaterIds ?? [],
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

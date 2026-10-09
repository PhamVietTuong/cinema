import { BarChartRow, StaffServiceAgent } from 'CinemaLib';

/** The API limit: a report range spans at most this many business dates, both ends included. */
export const MAX_REPORT_DAYS = 92;

const MS_PER_DAY = 86400000;

/** Why a date range cannot be reported on (an i18n key suffix under `salesReports.error`), or null when it is fine. */
export type RangeError = 'required' | 'order' | 'tooLong';

/** Parses a `yyyy-MM-dd` value from a date input as UTC midnight; null when empty or malformed. */
export function parseDay(value: string | null | undefined): Date | null {
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return null;
  }
  const date = new Date(value + 'T00:00:00Z');
  return isNaN(date.getTime()) ? null : date;
}

/** Number of business dates in the range, both ends included. */
export function rangeDays(from: Date, to: Date): number {
  return Math.round((to.getTime() - from.getTime()) / MS_PER_DAY) + 1;
}

/** Validates a date range the way the API will: both dates present, in order, and at most 92 days. */
export function validateRange(from: string | null | undefined, to: string | null | undefined): RangeError | null {
  const start = parseDay(from);
  const end = parseDay(to);
  if (!start || !end) {
    return 'required';
  }
  if (end < start) {
    return 'order';
  }
  return rangeDays(start, end) > MAX_REPORT_DAYS ? 'tooLong' : null;
}

/** `yyyy-MM-dd` of a date in UTC, for date inputs. */
export function toDayString(date: Date): string {
  return date.toISOString().slice(0, 10);
}

/** `yyyy-MM-dd` of the local calendar day `daysBack` days before `now` (the user's own "today"). */
export function localDayString(now: Date, daysBack = 0): string {
  const day = new Date(now.getFullYear(), now.getMonth(), now.getDate() - daysBack);
  const month = String(day.getMonth() + 1).padStart(2, '0');
  const date = String(day.getDate()).padStart(2, '0');
  return `${day.getFullYear()}-${month}-${date}`;
}

/** Request for the three report calls. A pinned theater manager always sends their own theater. */
export function buildReportRequest(
  from: string,
  to: string,
  groupBy: StaffServiceAgent.SalesGroupBy,
  theaterIds: readonly string[],
): StaffServiceAgent.StaffReportRequest {
  return StaffServiceAgent.StaffReportRequest.fromJS({
    from: parseDay(from),
    to: parseDay(to),
    groupBy,
    theaterIds: theaterIds.length > 0 ? [...theaterIds] : undefined,
  });
}

/** Bars of the sales chart: net revenue per group. */
export function salesChartRows(rows: readonly StaffServiceAgent.SalesReportRowDTO[], formatValue: (value: number) => string): BarChartRow[] {
  return rows.map(row => ({ label: row.label ?? row.key ?? '', value: row.netRevenue ?? 0, valueLabel: formatValue(row.netRevenue ?? 0) }));
}

/** Bars of the occupancy chart: one per row, scaled 0 to 1. */
export function occupancyChartRows(rows: readonly StaffServiceAgent.OccupancyRowDTO[], formatValue: (value: number) => string): BarChartRow[] {
  return rows.map(row => ({
    label: [row.theaterName, row.roomName, row.movieTitle].filter(Boolean).join(' / '),
    value: row.occupancyRate ?? 0,
    valueLabel: formatValue(row.occupancyRate ?? 0),
  }));
}

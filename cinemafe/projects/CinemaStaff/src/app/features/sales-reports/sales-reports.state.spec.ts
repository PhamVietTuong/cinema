import { CinemaServiceAgent } from 'CinemaLib';
import { MAX_REPORT_DAYS, buildReportRequest, localDayString, rangeDays, salesChartRows, toDayString, validateRange } from './sales-reports.state';

describe('validateRange', () => {
  it('requires both dates', () => {
    expect(validateRange('', '2026-10-04')).toBe('required');
    expect(validateRange('2026-10-01', null)).toBe('required');
    expect(validateRange('garbage', '2026-10-04')).toBe('required');
  });

  it('rejects an end before the start', () => {
    expect(validateRange('2026-10-05', '2026-10-04')).toBe('order');
  });

  it('accepts a single day and exactly 92 days', () => {
    expect(validateRange('2026-10-04', '2026-10-04')).toBeNull();
    expect(validateRange('2026-01-01', '2026-04-02')).toBeNull();
  });

  it('rejects 93 days', () => {
    expect(rangeDays(new Date('2026-01-01T00:00:00Z'), new Date('2026-04-03T00:00:00Z'))).toBe(MAX_REPORT_DAYS + 1);
    expect(validateRange('2026-01-01', '2026-04-03')).toBe('tooLong');
  });
});

describe('localDayString', () => {
  it('formats the local day and steps back across a month boundary', () => {
    const now = new Date(2026, 9, 4, 23, 30);
    expect(localDayString(now)).toBe('2026-10-04');
    expect(localDayString(now, 6)).toBe('2026-09-28');
  });
});

describe('buildReportRequest', () => {
  it('sends UTC midnight dates and omits an empty theater list', () => {
    const request = buildReportRequest('2026-10-01', '2026-10-04', CinemaServiceAgent.SalesGroupBy.Movie, []);
    expect(toDayString(request.from as Date)).toBe('2026-10-01');
    expect(request.theaterIds).toBeUndefined();
    expect(request.groupBy).toBe(CinemaServiceAgent.SalesGroupBy.Movie);
  });

  it('passes the selected theaters', () => {
    const request = buildReportRequest('2026-10-01', '2026-10-04', CinemaServiceAgent.SalesGroupBy.Day, ['t1', 't2']);
    expect(request.theaterIds).toEqual(['t1', 't2']);
  });
});

describe('salesChartRows', () => {
  it('maps net revenue per group', () => {
    const rows = [CinemaServiceAgent.SalesReportRowDTO.fromJS({ key: 'a', label: 'Movie A', netRevenue: 500 })];
    expect(salesChartRows(rows, v => v + 'd')).toEqual([{ label: 'Movie A', value: 500, valueLabel: '500d' }]);
  });
});

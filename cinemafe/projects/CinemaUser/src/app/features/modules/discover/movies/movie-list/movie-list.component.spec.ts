import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideMockStore } from '@ngrx/store/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { NEVER, of, throwError } from 'rxjs';
import { CinemaServiceAgent, LookupCacheService, selectMoviesLoading, selectPagedMovies } from 'CinemaLib';
import { MovieListComponent } from './movie-list.component';

/** getMovies totals per status ('' = the "all" tab). */
const MOVIE_TOTALS: Record<string, number> = { '': 100, now: 60, coming: 30 };

describe('MovieListComponent filters + tab counts', () => {
  let api: {
    getMovies: ReturnType<typeof vi.fn>;
    searchShowTimes: ReturnType<typeof vi.fn>;
    getMovieLanguages: ReturnType<typeof vi.fn>;
  };

  const build = () => {
    api = {
      getMovies: vi.fn((dto: { filters?: Record<string, string> }) => of({ results: [], totalCount: MOVIE_TOTALS[dto.filters?.['status'] ?? ''] })),
      searchShowTimes: vi.fn(() => of({ results: [], totalCount: 7 })),
      getMovieLanguages: vi.fn(() => of([])),
    };
    TestBed.configureTestingModule({
      providers: [
        provideMockStore({ selectors: [{ selector: selectMoviesLoading, value: false }, { selector: selectPagedMovies, value: { items: [], total: 0, page: 1 } }] }),
        provideTranslateService({ fallbackLang: 'vi' }),
        { provide: CinemaServiceAgent.HttpService, useValue: api },
        { provide: LookupCacheService, useValue: { movieTypes: () => of({ results: [] }), theaters: () => of({ results: [] }) } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) }, queryParamMap: NEVER } },
      ],
    });
    // Only the logic is under test, not the template.
    TestBed.overrideComponent(MovieListComponent, { set: { template: '', imports: [], styleUrl: undefined, styleUrls: [] } });
    const fixture = TestBed.createComponent(MovieListComponent);
    fixture.detectChanges();
    return fixture.componentInstance;
  };

  beforeEach(() => { vi.useFakeTimers(); });
  afterEach(() => { vi.useRealTimers(); });

  const flush = () => vi.advanceTimersByTime(600);

  it('loads a count for every tab on start with count-only requests (pageSize 1)', () => {
    const c = build();
    expect(c.counts).toEqual({ all: 100, now: 60, coming: 30, showtimes: 7 });
    for (const call of api.getMovies.mock.calls) { expect(call[0].pageSize).toBe(1); }
    expect(api.searchShowTimes.mock.calls[0][0].pageSize).toBe(1);
    // all + now + coming = 3 movie counts, 1 showtime count
    expect(api.getMovies).toHaveBeenCalledTimes(3);
    expect(api.searchShowTimes).toHaveBeenCalledTimes(1);
  });

  it('counts follow the active search and filters (showtimes ignores the search text)', () => {
    const c = build();
    api.getMovies.mockClear();
    api.searchShowTimes.mockClear();
    c.filterForm.patchValue({ movieTypeId: 'g1', language: 'Tiếng Việt' });
    c.searchCtrl.setValue('dune', { emitEvent: false });
    c.onFilterChange();
    flush();
    const statuses = api.getMovies.mock.calls.map(x => x[0].filters['status']);
    expect(statuses.length).toBe(3);
    expect(statuses).toEqual(expect.arrayContaining([undefined, 'coming', 'now']));
    for (const call of api.getMovies.mock.calls) {
      expect(call[0].filters).toMatchObject({ movieTypeId: 'g1', language: 'Tiếng Việt', search: 'dune' });
    }
    const st = api.searchShowTimes.mock.calls.at(-1)![0].filters;
    expect(st).toMatchObject({ movieTypeId: 'g1', language: 'Tiếng Việt' });
    expect(st['search']).toBeUndefined();
  });

  it('a burst of filter changes issues one round of count requests (debounced)', () => {
    const c = build();
    api.getMovies.mockClear();
    c.onFilterChange();
    c.onFilterChange();
    c.onFilterChange();
    flush();
    expect(api.getMovies).toHaveBeenCalledTimes(3);
  });

  it('a failing count hides only that badge', () => {
    const c = build();
    api.getMovies.mockImplementation((dto: { filters?: Record<string, string> }) => (dto.filters?.['status'] === 'now'
      ? throwError(() => new Error('boom'))
      : of({ results: [], totalCount: 5 })));
    c.onFilterChange();
    flush();
    expect(c.counts.now).toBeUndefined();
    expect(c.counts.all).toBe(5);
    expect(c.counts.coming).toBe(5);
  });

  it('keeps search and filters when switching tabs, dropping only the showtimes time range', () => {
    const c = build();
    c.searchCtrl.setValue('dune', { emitEvent: false });
    c.filterForm.patchValue({ movieTypeId: 'g1', theaterId: 't1', date: '2030-01-02' });

    c.setMode('showtimes');
    c.filterForm.patchValue({ timeFrom: '18:00', timeTo: '22:00' });
    expect(c.filterForm.getRawValue()).toMatchObject({ movieTypeId: 'g1', theaterId: 't1', date: '2030-01-02', timeFrom: '18:00', timeTo: '22:00' });
    expect(c.filterFields.some(f => f.type === 'time')).toBe(true);

    c.setMode('now');
    expect(c.filterForm.getRawValue()).toMatchObject({ movieTypeId: 'g1', theaterId: 't1', date: '2030-01-02', timeFrom: '', timeTo: '' });
    expect(c.searchCtrl.value).toBe('dune');
    expect(c.filterFields.some(f => f.type === 'time')).toBe(false);
    expect(c.hasActiveFilters).toBe(true);
  });

  it('the search text is not an active condition on the showtimes tab, and the time range counts once', () => {
    const c = build();
    c.searchCtrl.setValue('dune', { emitEvent: false });
    c.setMode('all');
    expect(c.activeCount).toBe(1);
    c.setMode('showtimes');
    expect(c.activeCount).toBe(0);
    expect(c.hasActiveFilters).toBe(false);
    c.filterForm.patchValue({ timeFrom: '10:00', timeTo: '12:00', language: 'Tiếng Anh' });
    expect(c.activeCount).toBe(2);
  });

  it('seeds the search box from ?q= when asked, replacing the previous text', () => {
    const c = build();
    c.setMode('all', 'matrix');
    expect(c.searchCtrl.value).toBe('matrix');
    c.setMode('now');
    expect(c.searchCtrl.value).toBe('matrix');
  });

  it('clearFilters empties search + every filter and refreshes the counts', () => {
    const c = build();
    c.searchCtrl.setValue('dune', { emitEvent: false });
    c.filterForm.patchValue({ movieTypeId: 'g1' });
    api.getMovies.mockClear();
    c.clearFilters();
    expect(c.searchCtrl.value).toBe('');
    expect(c.filterForm.getRawValue().movieTypeId).toBe('');
    expect(c.hasActiveFilters).toBe(false);
    expect(api.getMovies).toHaveBeenCalledTimes(3);
  });

  it('an inverted time range hides the showtimes badge without a request', () => {
    const c = build();
    c.setMode('showtimes');
    c.filterForm.patchValue({ timeFrom: '22:00', timeTo: '10:00' });
    api.searchShowTimes.mockClear();
    c.onFilterChange();
    flush();
    expect(c.counts.showtimes).toBeUndefined();
    expect(api.searchShowTimes).not.toHaveBeenCalled();
  });

  it('builds a 7-day date strip starting today', () => {
    const c = build();
    expect(c.dateStrip.length).toBe(7);
    const d = new Date();
    const pad = (n: number) => String(n).padStart(2, '0');
    expect(c.dateStrip[0].value).toBe(`${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`);
  });

  it('picking a strip day sets the date, picking it again clears it', () => {
    const c = build();
    const v = c.dateStrip[2].value;
    c.pickDate(v);
    expect(c.filterForm.value.date).toBe(v);
    c.pickDate(v);
    expect(c.filterForm.value.date).toBe('');
  });
});

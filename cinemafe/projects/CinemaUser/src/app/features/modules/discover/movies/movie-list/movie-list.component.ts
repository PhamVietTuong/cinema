import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { Observable, Subject, forkJoin, merge, of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, map, skip, switchMap, takeUntil } from 'rxjs/operators';
import { Store } from '@ngrx/store';
import { ActivatedRoute } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { FormControl, FormGroup } from '@angular/forms';
import {
  SharedModule, loadMovies, MOVIE_PAGE_SIZE, FilterPillsComponent, FilterBarField, FilterBarOption,
  selectPagedMovies, selectMoviesLoading, screeningFormatLabel, rawApiErrorMessage, CinemaServiceAgent,
  LookupCacheService,
} from 'CinemaLib';
import { MovieCardComponent } from '../../../../../shared/movie-card/movie-card.component';
import { EmptyStateComponent } from '../../../../../shared/empty-state/empty-state.component';
import { SiteFooterComponent } from '../../../../../shared/site-footer/site-footer.component';
import { movieDetailCommands } from '../../../../../shared/movie-route';

export type Mode = 'all' | 'now' | 'coming' | 'showtimes';

/** Page size of the showtime search results. */
const SHOWTIME_PAGE_SIZE = 20;

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-movie-list',
  standalone: true,
  imports: [SharedModule, MovieCardComponent, SiteFooterComponent, FilterPillsComponent, EmptyStateComponent],
  templateUrl: './movie-list.component.html',
  styleUrl: './movie-list.component.scss',
})
export class MovieListComponent implements OnInit, OnDestroy {
  private _store = inject(Store);
  private _route = inject(ActivatedRoute);
  private _cdr = inject(ChangeDetectorRef);
  private _api = inject(CinemaServiceAgent.HttpService);
  private _translate = inject(TranslateService);
  private _destroy$ = new Subject<void>();
  private _filterChange$ = new Subject<void>();
  private readonly _counts$ = new Subject<void>();

  readonly tabs: { mode: Mode; label: string }[] = [
    { mode: 'all', label: 'movies.tabs.all' },
    { mode: 'now', label: 'movies.tabs.now' },
    { mode: 'coming', label: 'movies.tabs.coming' },
    { mode: 'showtimes', label: 'movies.tabs.showtimes' },
  ];

  mode: Mode = 'all';
  searchCtrl = new FormControl('');
  /** Server-side filters shared by the movie list and the showtime search ('' = no filter). */
  readonly filterForm = new FormGroup({
    movieTypeId: new FormControl(''),
    language: new FormControl(''),
    theaterId: new FormControl(''),
    date: new FormControl(''),
    timeFrom: new FormControl(''),
    timeTo: new FormControl(''),
  });
  page = 1;
  pageSize = MOVIE_PAGE_SIZE;

  loading$ = this._store.select(selectMoviesLoading);

  private _genreOptions: FilterBarOption[] = [];
  private _languageOptions: FilterBarOption[] = [];
  private _theaterOptions: FilterBarOption[] = [];
  /** Filter-bar fields for the current mode. Rebuilt only when the mode or an option list changes,
   * so the OnPush filter bar is not handed a fresh array on every change detection. */
  filterFields: readonly FilterBarField[] = [];

  private _paged: any[] = [];
  private _pagedTotal = 0;

  /** Showtime search state (mode 'showtimes'). */
  showtimes: CinemaServiceAgent.ShowTimeSearchResultDTO[] = [];
  showtimesTotal = 0;
  showtimesLoading = false;
  showtimesError = '';
  private _showtimesRequest = 0;

  /** Result totals per tab for the current search + filters; a missing entry (not loaded yet / request failed) hides its badge. */
  counts: Partial<Record<Mode, number>> = {};
  /** Number of applied conditions (search counts, the time range counts once); drives "filtered by N conditions". */
  activeCount = 0;
  /** Quick-pick days of the showtimes date strip. */
  dateStrip: { value: string; label: string; sub: string }[] = [];
  /** Material icon per filter pill. */
  readonly pillIcons: Record<string, string> = { movieTypeId: 'sell', language: 'language', theaterId: 'place', date: 'event', time: 'schedule' };

  get isShowtimes(): boolean { return this.mode === 'showtimes'; }
  get title(): string {
    return this.mode === 'now' ? 'movies.list.titleNow'
      : this.mode === 'coming' ? 'movies.list.titleComing'
      : this.isShowtimes ? 'movies.list.titleShowtimes'
      : 'movies.list.titleAll';
  }
  get displayed(): any[] { return this._paged; }
  get total(): number { return this.isShowtimes ? this.showtimesTotal : this._pagedTotal; }
  get loadedCount(): number { return this.isShowtimes ? this.showtimes.length : this._paged.length; }
  get canLoadMore(): boolean { return this.loadedCount > 0 && this.loadedCount < this.total; }
  get isLoading(): boolean { return this.isShowtimes ? this.showtimesLoading : this._loading; }
  get loadProgressPct(): number {
    return this.total ? Math.min(100, (this.loadedCount / this.total) * 100) : 0;
  }
  private _loading = false;

  ngOnInit(): void {
    this.loading$.pipe(takeUntil(this._destroy$)).subscribe(l => { this._loading = l; this._cdr.markForCheck(); });
    this._store.select(selectPagedMovies).pipe(takeUntil(this._destroy$)).subscribe((p: any) => {
      const items = p?.items ?? [];
      // Keyed on the response's own page (not ambient component state), so a page-2
      // response that outlives a tab switch back to page 1 can't clobber the fresh list.
      this._paged = (p?.page ?? 1) === 1 ? items : [...this._paged, ...items];
      this._pagedTotal = p?.total ?? 0;
      this._cdr.markForCheck();
    });

    merge(this.searchCtrl.valueChanges, this.filterForm.valueChanges)
      .pipe(takeUntil(this._destroy$))
      .subscribe(() => this._refreshHasActiveFilters());

    this._loadFilterOptions();

    this._buildDateStrip();
    // Tab totals: one count-only request per tab (pageSize 1), restarted (switchMap) whenever search/filters/tab change.
    this._counts$.pipe(
      switchMap(() => forkJoin({
        all: this._count('all'), now: this._count('now'), coming: this._count('coming'), showtimes: this._count('showtimes'),
      })),
      takeUntil(this._destroy$),
    ).subscribe(c => {
      this.counts = c;
      this._cdr.markForCheck();
    });

    const showing = this._route.snapshot.queryParamMap.get('showing');
    // /movies?q=<text> (from the topbar search): seed the search box so the first load is filtered.
    const initialQuery = (this._route.snapshot.queryParamMap.get('q') ?? '').trim();
    this.setMode(showing === 'now' ? 'now' : showing === 'coming' ? 'coming' : showing === 'showtimes' ? 'showtimes' : 'all', initialQuery);

    // Searching again from the topbar while already on this page only changes the query string.
    this._route.queryParamMap.pipe(skip(1), takeUntil(this._destroy$)).subscribe(p => {
      const q = (p.get('q') ?? '').trim();
      if (this.isShowtimes) {
        this.setMode('all', q);
      } else if (q !== (this.searchCtrl.value ?? '').trim()) {
        this.searchCtrl.setValue(q);
      }
    });

    this.searchCtrl.valueChanges.pipe(debounceTime(400), distinctUntilChanged(), takeUntil(this._destroy$))
      .subscribe(() => { if (!this.isShowtimes) { this._reload(); } });

    this._filterChange$.pipe(debounceTime(250), takeUntil(this._destroy$))
      .subscribe(() => this._reload());
  }

  ngOnDestroy(): void { this._destroy$.next(); this._destroy$.complete(); }

  /** Switches tab. Search and filters stay; only the showtimes-only time range is dropped when leaving "showtimes".
   * `seedQuery` (from `?q=`) replaces the search text when given. */
  setMode(m: Mode, seedQuery?: string): void {
    this.mode = m;
    if (seedQuery !== undefined) {
      this.searchCtrl.setValue(seedQuery, { emitEvent: false });
    }
    if (m !== 'showtimes') {
      this.filterForm.patchValue({ timeFrom: '', timeTo: '' });
    }
    this._buildFilterFields();
    this._refreshHasActiveFilters();
    this._reload();
  }

  /** Arrow/Home/End move between tabs (roving tabindex) and activate the focused one. */
  onTabKey(ev: KeyboardEvent): void {
    const modes = this.tabs.map(t => t.mode);
    const i = modes.indexOf(this.mode);
    let next = i;
    if (ev.key === 'ArrowRight') { next = (i + 1) % modes.length; }
    else if (ev.key === 'ArrowLeft') { next = (i - 1 + modes.length) % modes.length; }
    else if (ev.key === 'Home') { next = 0; }
    else if (ev.key === 'End') { next = modes.length - 1; }
    else { return; }
    ev.preventDefault();
    this.setMode(modes[next]);
    const tablist = (ev.currentTarget as HTMLElement).parentElement;
    setTimeout(() => tablist?.querySelectorAll<HTMLElement>('[role="tab"]')[next]?.focus());
  }

  onFilterChange(): void {
    this._filterChange$.next();
  }

  clearFilters(): void {
    this.searchCtrl.reset('', { emitEvent: false });
    this.filterForm.reset({ movieTypeId: '', language: '', theaterId: '', date: '', timeFrom: '', timeTo: '' });
    this._refreshHasActiveFilters();
    this._reload();
  }

  /** Picks a day on the showtimes date strip (picking the applied day again clears it). */
  pickDate(value: string): void {
    const ctrl = this.filterForm.controls.date;
    ctrl.setValue(ctrl.value === value ? '' : value);
    this.onFilterChange();
  }

  /** True when the search box or any filter is set. Recomputed when the controls change (and after a
   * programmatic reset), not by a getter on every change-detection pass. */
  hasActiveFilters = false;

  private _refreshHasActiveFilters(): void {
    // The search box is hidden on the showtimes tab (it does not apply there), so its text is not an active condition.
    const searchOn = !this.isShowtimes && !!(this.searchCtrl.value ?? '').trim();
    const v = this.filterForm.getRawValue();
    this.activeCount = (searchOn ? 1 : 0) + [v.movieTypeId, v.language, v.theaterId, v.date].filter(x => !!x).length
      + (v.timeFrom || v.timeTo ? 1 : 0);
    this.hasActiveFilters = this.activeCount > 0;
    this._cdr.markForCheck();
  }

  /** Back to page 1 with the current search/filters, and refresh the tab counts. */
  private _reload(): void {
    this.page = 1;
    this._load();
    this._counts$.next();
  }

  /** Total of one tab for the current search + filters, via the ordinary list endpoints with pageSize 1.
   * Emits undefined (badge hidden) on error or for an invalid time range. */
  private _count(mode: Mode): Observable<number | undefined> {
    const filters = this._filterValues();
    if (mode === 'showtimes') {
      if (filters['timeFrom'] && filters['timeTo'] && filters['timeFrom'] > filters['timeTo']) { return of(undefined); }
      return this._api.searchShowTimes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 1, filters }))
        .pipe(map(r => r.totalCount ?? 0), catchError(() => of(undefined)));
    }
    // Time-of-day only exists on the showtimes tab.
    delete filters['timeFrom'];
    delete filters['timeTo'];
    const search = this.searchCtrl.value ?? '';
    if (search.trim()) { filters['search'] = search; }
    if (mode !== 'all') { filters['status'] = mode; }
    return this._api.getMovies(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 1, filters }))
      .pipe(map(r => r.totalCount ?? 0), catchError(() => of(undefined)));
  }

  private _buildDateStrip(): void {
    const lang = this._translate.currentLang() || 'vi';
    const locale = lang.startsWith('en') ? 'en-US' : 'vi-VN';
    const pad = (n: number): string => String(n).padStart(2, '0');
    this.dateStrip = [];
    for (let i = 0; i < 7; i++) {
      const d = new Date();
      d.setDate(d.getDate() + i);
      this.dateStrip.push({
        value: `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`,
        label: i === 0 ? this._translate.instant('clFilterPills.today')
          : i === 1 ? this._translate.instant('clFilterPills.tomorrow')
          : new Intl.DateTimeFormat(locale, { weekday: 'short' }).format(d),
        sub: `${pad(d.getDate())}/${pad(d.getMonth() + 1)}`,
      });
    }
  }

  private readonly _lookup = inject(LookupCacheService);

  private _loadFilterOptions(): void {
    this._lookup.movieTypes()
      .pipe(takeUntil(this._destroy$))
      .subscribe({
        next: r => {
          this._genreOptions = (r.results ?? []).map(g => ({ value: g.id ?? '', label: g.name ?? '' }));
          this._buildFilterFields();
        },
        error: () => {},
      });
    this._lookup.theaters()
      .pipe(takeUntil(this._destroy$))
      .subscribe({
        next: r => {
          this._theaterOptions = (r.results ?? []).filter(t => t.isActive !== false).map(t => ({ value: t.id ?? '', label: t.name ?? '' }));
          this._buildFilterFields();
        },
        error: () => {},
      });
    this._api.getMovieLanguages()
      .pipe(takeUntil(this._destroy$))
      .subscribe({
        next: languages => {
          this._languageOptions = (languages ?? []).map(l => ({ value: l, label: l }));
          this._buildFilterFields();
        },
        error: () => {},
      });
  }

  private _buildFilterFields(): void {
    const fields: FilterBarField[] = [
      { key: 'movieTypeId', type: 'select', labelKey: 'movies.filters.genre', options: this._genreOptions },
      { key: 'language', type: 'select', labelKey: 'movies.filters.language', options: this._languageOptions },
      { key: 'theaterId', type: 'select', labelKey: 'movies.filters.theater', options: this._theaterOptions },
      { key: 'date', type: 'date', labelKey: 'movies.filters.date' },
    ];
    if (this.isShowtimes) {
      fields.push(
        { key: 'timeFrom', type: 'time', labelKey: 'movies.filters.timeFrom' },
        { key: 'timeTo', type: 'time', labelKey: 'movies.filters.timeTo' },
      );
    }
    this.filterFields = fields;
    this._cdr.markForCheck();
  }

  private _filterValues(): Record<string, string> {
    const raw = this.filterForm.getRawValue() as Record<string, string | null>;
    const out: Record<string, string> = {};
    for (const [k, v] of Object.entries(raw)) {
      if (v) { out[k] = v; }
    }
    return out;
  }

  private _load(): void {
    if (this.isShowtimes) {
      this._loadShowtimes();
      return;
    }
    const f = this._filterValues();
    this._store.dispatch(loadMovies({
      filters: {
        search: this.searchCtrl.value ?? undefined,
        movieTypeId: f['movieTypeId'],
        language: f['language'],
        theaterId: f['theaterId'],
        date: f['date'],
        status: this.mode === 'now' ? 'now' : this.mode === 'coming' ? 'coming' : undefined,
      },
      page: this.page,
      pageSize: this.pageSize,
    }));
  }

  private _loadShowtimes(): void {
    const request = ++this._showtimesRequest;
    const filters: { [key: string]: string } = this._filterValues();
    // Same rule as the server (400 otherwise), checked here so the user gets a clear message at once.
    if (filters['timeFrom'] && filters['timeTo'] && filters['timeFrom'] > filters['timeTo']) {
      this.showtimes = [];
      this.showtimesTotal = 0;
      this.showtimesLoading = false;
      this.showtimesError = this._translate.instant('movies.filters.timeRangeInvalid');
      this._cdr.markForCheck();
      return;
    }
    this.showtimesLoading = true;
    this.showtimesError = '';
    this._cdr.markForCheck();
    this._api.searchShowTimes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: this.page, pageSize: SHOWTIME_PAGE_SIZE, filters }))
      .pipe(takeUntil(this._destroy$))
      .subscribe({
        next: r => {
          if (request !== this._showtimesRequest) { return; }
          this.showtimes = this.page === 1 ? (r.results ?? []) : [...this.showtimes, ...(r.results ?? [])];
          this.showtimesTotal = r.totalCount ?? 0;
          this.showtimesLoading = false;
          this._cdr.markForCheck();
        },
        error: err => {
          if (request !== this._showtimesRequest) { return; }
          if (this.page === 1) { this.showtimes = []; this.showtimesTotal = 0; }
          else { this.page -= 1; }
          // The server's own validation message, shown as plain text (not an i18n key).
          this.showtimesError = rawApiErrorMessage(err) ?? '';
          this.showtimesLoading = false;
          this._cdr.markForCheck();
        },
      });
  }

  loadMore(): void {
    this.page += 1;
    this._load();
  }

  formatOf(st: CinemaServiceAgent.ShowTimeSearchResultDTO): string {
    return screeningFormatLabel(st.roomTypeName, st.projectionForm);
  }

  movieLink(st: CinemaServiceAgent.ShowTimeSearchResultDTO): string[] {
    return movieDetailCommands(st.movieSlug ?? '');
  }
}

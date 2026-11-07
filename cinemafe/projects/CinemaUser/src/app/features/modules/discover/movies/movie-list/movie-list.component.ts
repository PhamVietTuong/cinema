import { ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, takeUntil } from 'rxjs/operators';
import { Store } from '@ngrx/store';
import { ActivatedRoute } from '@angular/router';
import { FormControl, FormGroup } from '@angular/forms';
import {
  SharedModule, loadMovies, MOVIE_PAGE_SIZE, FilterBarComponent, FilterBarField, FilterBarOption,
  selectPagedMovies, selectMoviesLoading, screeningFormatLabel, CinemaServiceAgent,
} from 'CinemaLib';
import { MovieCardComponent } from '../../../../../shared/movie-card/movie-card.component';
import { SiteFooterComponent } from '../../../../../shared/site-footer/site-footer.component';
import { movieDetailCommands } from '../../../../../shared/movie-route';

type Mode = 'all' | 'now' | 'coming' | 'showtimes';

/** Page size of the showtime search results. */
const SHOWTIME_PAGE_SIZE = 20;

@Component({
  selector: 'app-movie-list',
  standalone: true,
  imports: [SharedModule, MovieCardComponent, SiteFooterComponent, FilterBarComponent],
  templateUrl: './movie-list.component.html',
  styleUrl: './movie-list.component.scss',
})
export class MovieListComponent implements OnInit, OnDestroy {
  private _store = inject(Store);
  private _route = inject(ActivatedRoute);
  private _cdr = inject(ChangeDetectorRef);
  private _api = inject(CinemaServiceAgent.HttpService);
  private _destroy$ = new Subject<void>();
  private _filterChange$ = new Subject<void>();

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

    this._loadFilterOptions();

    const showing = this._route.snapshot.queryParamMap.get('showing');
    this.setMode(showing === 'now' ? 'now' : showing === 'coming' ? 'coming' : showing === 'showtimes' ? 'showtimes' : 'all');

    this.searchCtrl.valueChanges.pipe(debounceTime(400), distinctUntilChanged(), takeUntil(this._destroy$))
      .subscribe(() => { if (!this.isShowtimes) { this.page = 1; this._load(); } });

    this._filterChange$.pipe(debounceTime(250), takeUntil(this._destroy$))
      .subscribe(() => { this.page = 1; this._load(); });
  }

  ngOnDestroy(): void { this._destroy$.next(); this._destroy$.complete(); }

  setMode(m: Mode): void {
    this.mode = m;
    this.filterForm.reset({ movieTypeId: '', language: '', theaterId: '', date: '', timeFrom: '', timeTo: '' }, { emitEvent: false });
    this._buildFilterFields();
    this.page = 1;
    this._load();
  }

  onFilterChange(): void {
    this._filterChange$.next();
  }

  clearFilters(): void {
    this.filterForm.reset({ movieTypeId: '', language: '', theaterId: '', date: '', timeFrom: '', timeTo: '' }, { emitEvent: false });
    this.page = 1;
    this._load();
  }

  get hasActiveFilters(): boolean {
    return Object.values(this.filterForm.getRawValue()).some(v => !!v);
  }

  private _loadFilterOptions(): void {
    this._api.getMovieTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 100 }))
      .pipe(takeUntil(this._destroy$))
      .subscribe({
        next: r => {
          this._genreOptions = (r.results ?? []).map(g => ({ value: g.id ?? '', label: g.name ?? '' }));
          this._buildFilterFields();
        },
        error: () => {},
      });
    this._api.getTheaters(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 100 }))
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
          this.showtimesError = err?.error?.error ?? err?.message ?? '';
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

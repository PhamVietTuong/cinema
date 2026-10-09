import { ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, takeUntil } from 'rxjs/operators';
import { Store } from '@ngrx/store';
import { ActivatedRoute } from '@angular/router';
import { FormControl } from '@angular/forms';
import {
  SharedModule, loadMovies, loadNowShowing, loadComingSoon, MOVIE_PAGE_SIZE,
  selectPagedMovies, selectNowShowing, selectNowShowingTotal, selectComingSoon, selectComingSoonTotal, selectMoviesLoading,
} from 'CinemaLib';
import { MovieCardComponent } from '../../../../../shared/movie-card/movie-card.component';
import { SiteFooterComponent } from '../../../../../shared/site-footer/site-footer.component';

type Mode = 'all' | 'now' | 'coming';

@Component({
  selector: 'app-movie-list',
  standalone: true,
  imports: [SharedModule, MovieCardComponent, SiteFooterComponent],
  templateUrl: './movie-list.component.html',
  styleUrl: './movie-list.component.scss',
})
export class MovieListComponent implements OnInit, OnDestroy {
  private _store = inject(Store);
  private _route = inject(ActivatedRoute);
  private _cdr = inject(ChangeDetectorRef);
  private _destroy$ = new Subject<void>();

  readonly tabs: { mode: Mode; label: string }[] = [
    { mode: 'all', label: 'movies.tabs.all' },
    { mode: 'now', label: 'movies.tabs.now' },
    { mode: 'coming', label: 'movies.tabs.coming' },
  ];

  mode: Mode = 'all';
  selectedGenre = '';
  selectedLanguage = '';
  searchCtrl = new FormControl('');
  page = 1;
  pageSize = MOVIE_PAGE_SIZE;

  loading$ = this._store.select(selectMoviesLoading);

  private _now: any[] = [];
  private _nowTotal = 0;
  private _coming: any[] = [];
  private _comingTotal = 0;
  private _paged: any[] = [];
  private _pagedTotal = 0;

  get hasClientFilters(): boolean { return this.mode !== 'all'; }
  get title(): string {
    return this.mode === 'now' ? 'movies.list.titleNow' : this.mode === 'coming' ? 'movies.list.titleComing' : 'movies.list.titleAll';
  }
  private get _source(): any[] {
    return this.mode === 'now' ? this._now : this.mode === 'coming' ? this._coming : this._paged;
  }
  get total(): number {
    return this.mode === 'now' ? this._nowTotal : this.mode === 'coming' ? this._comingTotal : this._pagedTotal;
  }
  get loadedCount(): number { return this._source.length; }
  get canLoadMore(): boolean { return this.loadedCount > 0 && this.loadedCount < this.total; }
  get genres(): string[] {
    return [...new Set(this._source.flatMap(m => m.genres ?? []))].sort();
  }
  get languages(): string[] {
    return [...new Set(this._source.map(m => m.language).filter(Boolean))].sort();
  }
  get displayed(): any[] {
    return this._source.filter(m =>
      (!this.selectedGenre || (m.genres ?? []).includes(this.selectedGenre)) &&
      (!this.selectedLanguage || m.language === this.selectedLanguage));
  }
  get loadProgressPct(): number {
    return this.total ? Math.min(100, (this.loadedCount / this.total) * 100) : 0;
  }

  ngOnInit(): void {
    this._store.select(selectNowShowing).pipe(takeUntil(this._destroy$)).subscribe(l => { this._now = l ?? []; this._cdr.markForCheck(); });
    this._store.select(selectNowShowingTotal).pipe(takeUntil(this._destroy$)).subscribe(t => { this._nowTotal = t ?? 0; this._cdr.markForCheck(); });
    this._store.select(selectComingSoon).pipe(takeUntil(this._destroy$)).subscribe(l => { this._coming = l ?? []; this._cdr.markForCheck(); });
    this._store.select(selectComingSoonTotal).pipe(takeUntil(this._destroy$)).subscribe(t => { this._comingTotal = t ?? 0; this._cdr.markForCheck(); });
    this._store.select(selectPagedMovies).pipe(takeUntil(this._destroy$)).subscribe((p: any) => {
      const items = p?.items ?? [];
      // Keyed on the response's own page (not ambient component state), so a page-2
      // response that outlives a tab switch back to page 1 can't clobber the fresh list.
      this._paged = (p?.page ?? 1) === 1 ? items : [...this._paged, ...items];
      this._pagedTotal = p?.total ?? 0;
      this._cdr.markForCheck();
    });

    const showing = this._route.snapshot.queryParamMap.get('showing');
    this.setMode(showing === 'now' ? 'now' : showing === 'coming' ? 'coming' : 'all');

    this.searchCtrl.valueChanges.pipe(debounceTime(400), distinctUntilChanged(), takeUntil(this._destroy$))
      .subscribe(() => { if (this.mode === 'all') { this.page = 1; this._load(); } });
  }

  ngOnDestroy(): void { this._destroy$.next(); this._destroy$.complete(); }

  setMode(m: Mode): void {
    this.mode = m;
    this.selectedGenre = '';
    this.selectedLanguage = '';
    this.page = 1;
    this._load();
  }

  private _load(): void {
    if (this.mode === 'now') { this._store.dispatch(loadNowShowing({ page: this.page, pageSize: this.pageSize })); }
    else if (this.mode === 'coming') { this._store.dispatch(loadComingSoon({ page: this.page, pageSize: this.pageSize })); }
    else { this._store.dispatch(loadMovies({ search: this.searchCtrl.value ?? undefined, page: this.page, pageSize: this.pageSize })); }
  }

  loadMore(): void {
    this.page += 1;
    this._load();
  }
}

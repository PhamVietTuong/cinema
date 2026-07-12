import { ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, takeUntil } from 'rxjs/operators';
import { Store } from '@ngrx/store';
import { ActivatedRoute } from '@angular/router';
import { FormControl } from '@angular/forms';
import { PageEvent } from '@angular/material/paginator';
import {
  SharedModule, loadMovies, loadNowShowing, loadComingSoon,
  selectPagedMovies, selectNowShowing, selectComingSoon, selectMoviesLoading,
} from 'CinemaLib';

type Mode = 'all' | 'now' | 'coming';

@Component({
  selector: 'app-movie-list',
  standalone: true,
  imports: [SharedModule],
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
  pageSize = 12;
  total = 0;

  loading$ = this._store.select(selectMoviesLoading);

  private _now: any[] = [];
  private _coming: any[] = [];
  private _paged: any[] = [];

  get isPaged(): boolean { return this.mode === 'all'; }
  get title(): string {
    return this.mode === 'now' ? 'movies.list.titleNow' : this.mode === 'coming' ? 'movies.list.titleComing' : 'movies.list.titleAll';
  }
  private get _source(): any[] {
    return this.mode === 'now' ? this._now : this.mode === 'coming' ? this._coming : this._paged;
  }
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

  ngOnInit(): void {
    this._store.select(selectNowShowing).pipe(takeUntil(this._destroy$)).subscribe(l => { this._now = l ?? []; this._cdr.markForCheck(); });
    this._store.select(selectComingSoon).pipe(takeUntil(this._destroy$)).subscribe(l => { this._coming = l ?? []; this._cdr.markForCheck(); });
    this._store.select(selectPagedMovies).pipe(takeUntil(this._destroy$)).subscribe((p: any) => { this._paged = p?.items ?? []; this.total = p?.total ?? 0; this._cdr.markForCheck(); });

    const showing = this._route.snapshot.queryParamMap.get('showing');
    this.setMode(showing === 'now' ? 'now' : showing === 'coming' ? 'coming' : 'all');

    this.searchCtrl.valueChanges.pipe(debounceTime(400), distinctUntilChanged(), takeUntil(this._destroy$))
      .subscribe(() => { if (this.mode === 'all') { this.page = 1; this._loadPaged(); } });
  }

  ngOnDestroy(): void { this._destroy$.next(); this._destroy$.complete(); }

  setMode(m: Mode): void {
    this.mode = m;
    this.selectedGenre = '';
    this.selectedLanguage = '';
    if (m === 'now') { this._store.dispatch(loadNowShowing()); }
    else if (m === 'coming') { this._store.dispatch(loadComingSoon()); }
    else { this.page = 1; this._loadPaged(); }
  }

  private _loadPaged(): void {
    this._store.dispatch(loadMovies({ search: this.searchCtrl.value ?? undefined, page: this.page, pageSize: this.pageSize }));
  }

  onPageChange(e: PageEvent): void {
    this.page = e.pageIndex + 1;
    this.pageSize = e.pageSize;
    this._loadPaged();
  }
}

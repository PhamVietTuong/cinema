import { Component, OnInit, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { map, debounceTime, distinctUntilChanged } from 'rxjs/operators';
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
export class MovieListComponent implements OnInit {
  private _store = inject(Store);
  private _route = inject(ActivatedRoute);

  mode: Mode = 'all';
  movies$!: Observable<any[]>;
  pagedMovies$ = this._store.select(selectPagedMovies);
  loading$ = this._store.select(selectMoviesLoading);
  searchCtrl = new FormControl('');
  page = 1;
  pageSize = 12;

  get title(): string {
    return this.mode === 'now' ? 'Phim Đang Chiếu'
      : this.mode === 'coming' ? 'Phim Sắp Chiếu'
      : 'Tất Cả Phim';
  }
  get isPaged(): boolean { return this.mode === 'all'; }

  ngOnInit(): void {
    const showing = this._route.snapshot.queryParamMap.get('showing');
    this.mode = showing === 'now' ? 'now' : showing === 'coming' ? 'coming' : 'all';

    if (this.mode === 'now') {
      this._store.dispatch(loadNowShowing());
      this.movies$ = this._store.select(selectNowShowing);
    } else if (this.mode === 'coming') {
      this._store.dispatch(loadComingSoon());
      this.movies$ = this._store.select(selectComingSoon);
    } else {
      this.movies$ = this.pagedMovies$.pipe(map((p: any) => p?.items ?? []));
      this.loadMovies();
      this.searchCtrl.valueChanges.pipe(debounceTime(400), distinctUntilChanged()).subscribe(() => {
        this.page = 1;
        this.loadMovies();
      });
    }
  }

  loadMovies(): void {
    this._store.dispatch(loadMovies({ search: this.searchCtrl.value ?? undefined, page: this.page, pageSize: this.pageSize }));
  }

  onPageChange(e: PageEvent): void {
    this.page = e.pageIndex + 1;
    this.pageSize = e.pageSize;
    this.loadMovies();
  }
}

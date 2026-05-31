import { Component, OnInit } from '@angular/core';
import { Observable } from 'rxjs';
import { Store } from '@ngrx/store';
import { FormControl } from '@angular/forms';
import { PageEvent } from '@angular/material/paginator';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { SharedModule, loadMovies, selectPagedMovies, selectMoviesLoading } from 'CinemaLib';

@Component({
  selector: 'app-movie-list',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './movie-list.component.html',
  styleUrl: './movie-list.component.scss',
})
export class MovieListComponent implements OnInit {
  pagedMovies$: Observable<any>;
  loading$: Observable<boolean>;

  constructor(private _store: Store) {
    this.pagedMovies$ = this._store.select(selectPagedMovies);
    this.loading$ = this._store.select(selectMoviesLoading);
  }
  searchCtrl = new FormControl('');
  page = 1;
  pageSize = 12;

  ngOnInit(): void {
    this.loadMovies();
    this.searchCtrl.valueChanges.pipe(debounceTime(400), distinctUntilChanged()).subscribe(() => {
      this.page = 1;
      this.loadMovies();
    });
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

import { Component, OnInit } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { Store } from '@ngrx/store';
import {
  SharedModule,
  loadNowShowing, loadComingSoon,
  selectNowShowing, selectComingSoon, selectMoviesLoading,
} from 'CinemaLib';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
})
export class HomeComponent implements OnInit {
  nowShowing$: Observable<any[]>;
  comingSoon$: Observable<any[]>;
  loading$: Observable<boolean>;
  featuredMovie$: Observable<any>;

  constructor(private _store: Store) {
    this.nowShowing$ = this._store.select(selectNowShowing);
    this.comingSoon$ = this._store.select(selectComingSoon);
    this.loading$ = this._store.select(selectMoviesLoading);
    this.featuredMovie$ = this.nowShowing$.pipe(map(movies => movies?.[0] ?? null));
  }

  ngOnInit(): void {
    this._store.dispatch(loadNowShowing());
    this._store.dispatch(loadComingSoon());
  }

  /** Falls back to the bundled placeholder when a poster URL fails to load. */
  onImgError(e: Event): void {
    const img = e.target as HTMLImageElement;
    if (!img.src.endsWith('assets/no-poster.jpg')) {
      img.src = 'assets/no-poster.jpg';
    }
  }
}

import { Component, OnInit } from '@angular/core';
import { Observable } from 'rxjs';
import { ActivatedRoute } from '@angular/router';
import { Store } from '@ngrx/store';
import { SharedModule, loadMovieDetail, rateMovie, addComment, selectSelectedMovie, selectMoviesLoading, selectIsAuthenticated } from 'CinemaLib';

@Component({
  selector: 'app-movie-detail',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './movie-detail.component.html',
  styleUrl: './movie-detail.component.scss',
})
export class MovieDetailComponent implements OnInit {
  movie$: Observable<any>;
  loading$: Observable<boolean>;
  isAuthenticated$: Observable<boolean>;

  movieId = '';
  /** Star rating the user is about to submit (1–10; 0 = none picked). */
  myScore = 0;
  myReview = '';
  newComment = '';
  readonly stars = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

  constructor(private _store: Store, private _route: ActivatedRoute) {
    this.movie$ = this._store.select(selectSelectedMovie);
    this.loading$ = this._store.select(selectMoviesLoading);
    this.isAuthenticated$ = this._store.select(selectIsAuthenticated);
  }

  ngOnInit(): void {
    this.movieId = this._route.snapshot.paramMap.get('id') ?? '';
    this._store.dispatch(loadMovieDetail({ id: this.movieId }));
  }

  setScore(n: number): void { this.myScore = n; }

  submitRating(): void {
    if (this.myScore < 1) { return; }
    this._store.dispatch(rateMovie({ movieId: this.movieId, score: this.myScore, review: this.myReview.trim() || undefined }));
    this.myReview = '';
  }

  submitComment(): void {
    const content = this.newComment.trim();
    if (!content) { return; }
    this._store.dispatch(addComment({ movieId: this.movieId, content }));
    this.newComment = '';
  }

  getCastMembers(cast: string | undefined): string[] {
    return cast ? cast.split(',').map(s => s.trim()).filter(Boolean).slice(0, 6) : [];
  }

  getInitials(name: string): string {
    return name.split(' ').filter(Boolean).map(w => w[0]).join('').toUpperCase().slice(0, 2);
  }

  scrollToShowtimes(): void {
    document.getElementById('showtimes')?.scrollIntoView({ behavior: 'smooth' });
  }

  formatLabel(f?: number): string {
    return f === 3 ? 'IMAX' : f === 2 ? '3D' : '2D';
  }

  /** Groups a movie's showtimes by projection format for the "Lịch Chiếu" section. */
  groupByFormat(showTimes: any[] | undefined): { label: string; items: any[] }[] {
    const map = new Map<number, any[]>();
    for (const st of showTimes ?? []) {
      const f = st.projectionForm ?? 1;
      if (!map.has(f)) { map.set(f, []); }
      map.get(f)!.push(st);
    }
    return [...map.entries()]
      .sort((a, b) => a[0] - b[0])
      .map(([f, items]) => ({
        label: this.formatLabel(f),
        items: items.sort((x, y) => new Date(x.startTime).getTime() - new Date(y.startTime).getTime()),
      }));
  }
}

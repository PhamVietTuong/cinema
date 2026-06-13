import { Component, OnInit } from '@angular/core';
import { Observable } from 'rxjs';
import { ActivatedRoute } from '@angular/router';
import { Store } from '@ngrx/store';
import { SharedModule, loadMovieDetail, selectSelectedMovie, selectMoviesLoading } from 'CinemaLib';

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

  constructor(private _store: Store, private _route: ActivatedRoute) {
    this.movie$ = this._store.select(selectSelectedMovie);
    this.loading$ = this._store.select(selectMoviesLoading);
  }

  ngOnInit(): void {
    const id = this._route.snapshot.paramMap.get('id') ?? '';
    this._store.dispatch(loadMovieDetail({ id }));
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

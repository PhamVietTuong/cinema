import { ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, Input, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { SharedModule, CinemaServiceAgent, toDateKey } from 'CinemaLib';

interface TheaterOption { id: string; name: string; }
interface DateOption { key: string; date: Date; }

/**
 * Home "quick booking" bar: movie -> theater -> date -> showtime, then straight to seat selection.
 * Showtimes come from the movie's public detail payload (same source as the movie-detail page), so no
 * extra endpoint is needed. Each step stays disabled until the previous one is chosen.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-quick-booking',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './quick-booking.component.html',
  styleUrl: './quick-booking.component.scss',
})
export class QuickBookingComponent {
  private readonly _cinema = inject(CinemaServiceAgent.HttpService);
  private readonly _router = inject(Router);
  private readonly _cdr = inject(ChangeDetectorRef);
  private readonly _destroyRef = inject(DestroyRef);

  /** Now-showing movies to pick from. */
  @Input() movies: CinemaServiceAgent.MovieDTO[] | null = [];

  movieSlug = '';
  theaterId = '';
  dateKey = '';
  showTimeId = '';

  loadingShowTimes = false;
  loadFailed = false;
  private _showTimes: CinemaServiceAgent.ShowTimeSummaryDTO[] = [];
  private _loadSeq = 0;

  get theaters(): TheaterOption[] {
    const seen = new Map<string, TheaterOption>();
    for (const st of this._showTimes) {
      if (st.theaterId && !seen.has(st.theaterId)) {
        seen.set(st.theaterId, { id: st.theaterId, name: st.theaterName ?? '' });
      }
    }
    return [...seen.values()];
  }

  get dates(): DateOption[] {
    const seen = new Map<string, DateOption>();
    for (const st of this._forTheater()) {
      const key = this._dateKey(st.startTime!);
      if (!seen.has(key)) {
        seen.set(key, { key, date: new Date(st.startTime!) });
      }
    }
    return [...seen.values()];
  }

  get times(): CinemaServiceAgent.ShowTimeSummaryDTO[] {
    return this._forTheater().filter(st => this._dateKey(st.startTime!) === this.dateKey);
  }

  get canBook(): boolean {
    return !!this.showTimeId;
  }

  onMovie(slug: string): void {
    this.movieSlug = slug;
    this.theaterId = '';
    this.dateKey = '';
    this.showTimeId = '';
    this._showTimes = [];
    this.loadFailed = false;
    const seq = ++this._loadSeq;
    if (!slug) {
      this.loadingShowTimes = false;
      return;
    }
    this.loadingShowTimes = true;
    this._cinema.getMovieBySlug(slug).pipe(takeUntilDestroyed(this._destroyRef)).subscribe({
      next: m => {
        if (seq !== this._loadSeq) {
          return;
        }
        const now = Date.now();
        this._showTimes = (m.showTimes ?? [])
          .filter(st => st.startTime && new Date(st.startTime).getTime() > now)
          .sort((a, b) => new Date(a.startTime!).getTime() - new Date(b.startTime!).getTime());
        this.loadingShowTimes = false;
        this._cdr.markForCheck();
      },
      error: () => {
        if (seq !== this._loadSeq) {
          return;
        }
        this.loadingShowTimes = false;
        this.loadFailed = true;
        this._cdr.markForCheck();
      },
    });
  }

  onTheater(id: string): void {
    this.theaterId = id;
    this.dateKey = '';
    this.showTimeId = '';
  }

  onDate(key: string): void {
    this.dateKey = key;
    this.showTimeId = '';
  }

  onTime(id: string): void {
    this.showTimeId = id;
  }

  book(): void {
    const st = this._showTimes.find(x => x.id === this.showTimeId);
    if (!st) {
      return;
    }
    this._router.navigate(['/booking/seats'], { queryParams: { showTimeId: st.id, roomId: st.roomId } });
  }

  private _forTheater(): CinemaServiceAgent.ShowTimeSummaryDTO[] {
    return this._showTimes.filter(st => st.theaterId === this.theaterId);
  }

  /** Local (not UTC) yyyy-MM-dd, matching movie-detail's date bucketing. */
  private _dateKey(value: string | Date): string {
    return toDateKey(new Date(value));
  }
}

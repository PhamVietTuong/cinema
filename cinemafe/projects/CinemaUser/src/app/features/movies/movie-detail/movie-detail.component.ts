import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { Observable } from 'rxjs';
import { take } from 'rxjs/operators';
import { ActivatedRoute, Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { SharedModule, loadMovieDetail, rateMovie, addComment, selectSelectedMovie, selectMoviesLoading, selectIsAuthenticated, screeningFormatLabel, ToastService } from 'CinemaLib';
import { BookingSelectionComponent } from '../../booking/booking-selection/booking-selection.component';

@Component({
  selector: 'app-movie-detail',
  standalone: true,
  imports: [SharedModule, BookingSelectionComponent],
  templateUrl: './movie-detail.component.html',
  styleUrl: './movie-detail.component.scss',
})
export class MovieDetailComponent implements OnInit {
  movie$: Observable<any>;
  loading$: Observable<boolean>;
  isAuthenticated$: Observable<boolean>;

  movieId = '';
  /** The current user's rating, once submitted (1–10; 0 = none submitted yet this session). */
  myScore = 0;
  /** Hovered star while picking a rating in the hero widget; 0 when not hovering. */
  hoverScore = 0;
  newComment = '';
  readonly heroStars = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

  /** Today + the next 3 days — the movie-detail response already covers exactly this window
   * (MovieManager.GetDetailAsync), so switching tabs is a client-side filter, no re-fetch. */
  readonly dateTabs: { key: string; date: Date }[] = [];
  selectedDateKey = '';

  /** The showtime the inline booking panel is currently targeting; empty when no panel is open.
   * Clicking a different chip re-targets BookingSelectionComponent instead of navigating away. */
  selectedShowTimeId = '';
  selectedRoomId = '';
  selectedShowTimeLabel = '';

  constructor(
    private _store: Store,
    private _route: ActivatedRoute,
    private _router: Router,
    private _cdr: ChangeDetectorRef,
    private _translate: TranslateService,
    private _toast: ToastService,
  ) {
    this.movie$ = this._store.select(selectSelectedMovie);
    this.loading$ = this._store.select(selectMoviesLoading);
    this.isAuthenticated$ = this._store.select(selectIsAuthenticated);
  }

  ngOnInit(): void {
    this.movieId = this._route.snapshot.paramMap.get('id') ?? '';
    this._store.dispatch(loadMovieDetail({ id: this.movieId }));

    this.dateTabs.push(...this.buildDateTabs());
    this.selectedDateKey = this.dateTabs[0].key;
  }

  private buildDateTabs(): { key: string; date: Date }[] {
    const tabs: { key: string; date: Date }[] = [];
    for (let i = 0; i < 4; i++) {
      const date = new Date();
      date.setDate(date.getDate() + i);
      tabs.push({ key: this.toDateKey(date), date });
    }
    return tabs;
  }

  /** Local (not UTC) yyyy-MM-dd — a showtime near midnight must bucket to the day it's shown on
   * the theater's wall clock, not shift a day via a UTC string conversion. */
  private toDateKey(value: string | Date): string {
    const d = new Date(value);
    const y = d.getFullYear();
    const m = (d.getMonth() + 1).toString().padStart(2, '0');
    const day = d.getDate().toString().padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  selectDate(key: string): void {
    this.selectedDateKey = key;
    // The panel is pinned to a showtime chip above it; switching dates hides that chip (a different
    // date's showtimes render instead), so keeping the panel open would orphan it from the list.
    this.closeBookingPanel();
  }

  /**
   * Opens (or re-targets) the inline booking panel for the clicked showtime. Anonymous visitors may
   * browse seats and food too (the seat map and prices are public reads); login is only required at
   * checkout, where BookingSelectionComponent parks the order and resumes it after sign-in.
   */
  selectShowTime(st: any): void {
    this.selectedShowTimeId = st.id;
    this.selectedRoomId = st.roomId;
    const time = new Date(st.startTime);
    const hh = time.getHours().toString().padStart(2, '0');
    const mm = time.getMinutes().toString().padStart(2, '0');
    const format = screeningFormatLabel(st.roomTypeName, st.projectionForm);
    this.selectedShowTimeLabel = `${st.theaterName} · ${hh}:${mm} · ${format}`;
    this._cdr.markForCheck();
    setTimeout(() => this._scrollToBookingPanel(), 0);
  }

  closeBookingPanel(): void {
    this.selectedShowTimeId = '';
    this.selectedRoomId = '';
    this.selectedShowTimeLabel = '';
    this._cdr.markForCheck();
  }

  private _scrollToBookingPanel(): void {
    document.getElementById('inline-booking')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  /** Number of showtimes on the given local date — used for each date tab's count badge. */
  countForDate(showTimes: any[] | undefined, key: string): number {
    return (showTimes ?? []).filter(st => this.toDateKey(st.startTime) === key).length;
  }

  /**
   * Groups a movie's showtimes, for the selected date only, by cinema (theater id — not name, so two
   * distinct theaters that happen to share a display name never collapse into one group), sorted by
   * theater name; within each cinema, sub-grouped by screening format via groupByFormat. Each group
   * also carries the theater's name/address, read off its first showtime (every showtime in a group
   * shares the same theater).
   */
  groupByTheater(showTimes: any[] | undefined, key: string):
    { theaterId: string; theaterName: string; theaterAddress: string; formats: { label: string; items: any[] }[] }[] {
    const forDate = (showTimes ?? []).filter(st => this.toDateKey(st.startTime) === key);
    const map = new Map<string, any[]>();
    for (const st of forDate) {
      const id = st.theaterId ?? '';
      if (!map.has(id)) { map.set(id, []); }
      map.get(id)!.push(st);
    }
    return [...map.entries()]
      .map(([theaterId, items]) => ({
        theaterId,
        theaterName: items[0].theaterName ?? '',
        theaterAddress: items[0].theaterAddress ?? '',
        formats: this.groupByFormat(items),
      }))
      .sort((a, b) => a.theaterName.localeCompare(b.theaterName));
  }

  /** Score to paint in the hero widget: the real average once rated, else a full 10/10 default. */
  displayScore(movie: any): number {
    return movie.ratingCount > 0 ? movie.averageRating : 10;
  }

  onHeroStarHover(score: number): void {
    this.hoverScore = score;
    this._cdr.markForCheck();
  }

  onHeroStarLeave(): void {
    this.hoverScore = 0;
    this._cdr.markForCheck();
  }

  /** Submits immediately on star click — no separate confirm button. RateMovieAsync is an upsert,
   * so re-clicking just updates this user's existing rating rather than adding a duplicate. */
  rateFromHero(score: number): void {
    this.isAuthenticated$.pipe(take(1)).subscribe(isAuthenticated => {
      if (!isAuthenticated) {
        this._router.navigate(['/auth/login'], { queryParams: { returnUrl: '/movies/' + this.movieId } });
        return;
      }
      this._store.dispatch(rateMovie({ movieId: this.movieId, score, review: undefined }));
      this.myScore = score;
      this._toast.success(this._translate.instant('movies.detail.ratingSaved'));
      this._cdr.markForCheck();
    });
  }

  commentCount(movie: any): number {
    return (movie.recentComments ?? []).reduce((n: number, c: any) => n + 1 + (c.replies?.length ?? 0), 0);
  }

  submitComment(): void {
    const content = this.newComment.trim();
    if (!content) { return; }
    this.isAuthenticated$.pipe(take(1)).subscribe(isAuthenticated => {
      if (!isAuthenticated) {
        this._router.navigate(['/auth/login'], { queryParams: { returnUrl: '/movies/' + this.movieId } });
        return;
      }
      this._store.dispatch(addComment({ movieId: this.movieId, content }));
      this.newComment = '';
      this._toast.success(this._translate.instant('movies.detail.commentPosted'));
      this._cdr.markForCheck();
    });
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

  /**
   * Groups a movie's showtimes for the "Lich Chieu" section by the label a customer books against:
   * the room class plus the dimension ("IMAX 2D", "IMAX 3D", "2D"). Those are two independent axes,
   * so one hall can appear under more than one group across the day.
   */
  groupByFormat(showTimes: any[] | undefined): { label: string; items: any[] }[] {
    const map = new Map<string, any[]>();
    for (const st of showTimes ?? []) {
      const label = screeningFormatLabel(st.roomTypeName, st.projectionForm);
      if (!map.has(label)) { map.set(label, []); }
      map.get(label)!.push(st);
    }
    return [...map.entries()]
      .sort((a, b) => a[0].localeCompare(b[0]))
      .map(([label, items]) => ({
        label,
        items: items.sort((x, y) => new Date(x.startTime).getTime() - new Date(y.startTime).getTime()),
      }));
  }
}

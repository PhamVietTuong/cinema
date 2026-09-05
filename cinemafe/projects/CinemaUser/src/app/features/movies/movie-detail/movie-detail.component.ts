import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { Observable } from 'rxjs';
import { take } from 'rxjs/operators';
import { ActivatedRoute, Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { SharedModule, loadMovieDetail, rateMovie, addComment, selectSelectedMovie, selectMoviesLoading, selectIsAuthenticated, screeningFormatLabel } from 'CinemaLib';
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
  /** Star rating the user is about to submit (1–10; 0 = none picked). */
  myScore = 0;
  myReview = '';
  newComment = '';
  readonly stars = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

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
   * Opens (or re-targets) the inline booking panel for the clicked showtime. Anonymous visitors are
   * sent to log in instead — the booking hub and its seat locks require an authenticated connection,
   * and silently showing a panel that can't actually lock seats would fail invisibly at checkout.
   */
  selectShowTime(st: any): void {
    this.isAuthenticated$.pipe(take(1)).subscribe(isAuthenticated => {
      if (!isAuthenticated) {
        this._router.navigate(['/auth/login'], { queryParams: { returnUrl: '/movies/' + this.movieId } });
        return;
      }
      this.selectedShowTimeId = st.id;
      this.selectedRoomId = st.roomId;
      const time = new Date(st.startTime);
      const hh = time.getHours().toString().padStart(2, '0');
      const mm = time.getMinutes().toString().padStart(2, '0');
      const format = screeningFormatLabel(st.roomTypeName, st.projectionForm);
      this.selectedShowTimeLabel = `${st.theaterName} · ${hh}:${mm} · ${format}`;
      this._cdr.markForCheck();
      setTimeout(() => this._scrollToBookingPanel(), 0);
    });
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
   * Groups a movie's showtimes, for the selected date only, by cinema (theater), sorted
   * alphabetically; within each cinema, sub-grouped by screening format via groupByFormat.
   */
  groupByTheater(showTimes: any[] | undefined, key: string): { theaterName: string; formats: { label: string; items: any[] }[] }[] {
    const forDate = (showTimes ?? []).filter(st => this.toDateKey(st.startTime) === key);
    const map = new Map<string, any[]>();
    for (const st of forDate) {
      const name = st.theaterName ?? '';
      if (!map.has(name)) { map.set(name, []); }
      map.get(name)!.push(st);
    }
    return [...map.entries()]
      .sort((a, b) => a[0].localeCompare(b[0]))
      .map(([theaterName, items]) => ({ theaterName, formats: this.groupByFormat(items) }));
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

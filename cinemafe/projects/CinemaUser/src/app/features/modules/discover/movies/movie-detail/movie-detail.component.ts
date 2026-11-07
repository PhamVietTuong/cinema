import { ChangeDetectorRef, Component, DestroyRef, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Location } from '@angular/common';
import { Observable } from 'rxjs';
import { filter, take } from 'rxjs/operators';
import { ActivatedRoute, Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { Actions, ofType } from '@ngrx/effects';
import { TranslateService } from '@ngx-translate/core';
import { SharedModule, loadMovieDetail, loadMovieDetailBySlug, rateMovie, rateMovieSuccess, rateMovieFailure, addComment, addCommentSuccess, addCommentFailure, updateOwnComment, updateOwnCommentSuccess, updateOwnCommentFailure, deleteOwnComment, deleteOwnCommentSuccess, deleteOwnCommentFailure, selectSelectedMovie, selectMoviesLoading, selectIsAuthenticated, screeningFormatLabel, rottenTomatoesTier, ToastService, DialogService, CinemaServiceAgent } from 'CinemaLib';
import { BookingSelectionComponent } from '../../../booking/booking-selection/booking-selection.component';
import { BookingCheckoutState } from '../../../booking/booking-checkout/booking-checkout.state';
import { movieDetailUrl, isLegacyMovieId } from '../../../../../shared/movie-route';
import { ScrollRowComponent } from '../../../../../shared/scroll-row/scroll-row.component';

@Component({
  selector: 'app-movie-detail',
  standalone: true,
  imports: [SharedModule, BookingSelectionComponent, ScrollRowComponent],
  templateUrl: './movie-detail.component.html',
  styleUrl: './movie-detail.component.scss',
})
export class MovieDetailComponent implements OnInit {
  movie$: Observable<any>;
  loading$: Observable<boolean>;
  isAuthenticated$: Observable<boolean>;

  movieId = '';
  /** Canonical `/movies/:slug` URL for the loaded movie. Used for the login `returnUrl` so it always
   * points at the friendly URL, not the raw path. */
  detailUrl = '';
  /** The route param as last written into the address bar: either the slug, or (until resolved) a
   * legacy GUID. Kept separately because `Location.replaceState` does not update the router's
   * snapshot/url. */
  private _routeParam = '';
  /** The current user's rating, once submitted (1–10; 0 = none submitted yet this session). */
  myScore = 0;
  /** Hovered star while picking a rating in the hero widget; 0 when not hovering. */
  hoverScore = 0;
  newComment = '';
  /** The user's private review text, seeded from the server's `myReview` on every detail load. */
  reviewText = '';
  readonly maxCommentLength = 2000;
  readonly maxReviewLength = 1000;
  /** Matches the backend's per-page size of top-level comments. */
  private readonly _commentPageSize = 10;
  /** Extra top-level comments fetched by "load more", appended after `movie.recentComments`. */
  extraComments: any[] = [];
  commentsHasMore = false;
  commentsLoadingMore = false;
  private _commentsPage = 1;
  /** Movie whose extra comment pages are loaded; they reset only when this changes. */
  private _commentsMovieId = '';
  /** Bumped on every detail (re)load; a load-more response begun before the latest bump is stale. */
  private _detailSeq = 0;
  /** Comment id whose reply form is open (top-level comments only: replies are one level deep). */
  replyingToId = '';
  replyText = '';
  /** Comment id being edited inline. */
  editingId = '';
  editText = '';
  readonly heroStars = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
  readonly rtTier = rottenTomatoesTier;

  /** Today + the next 3 days — the movie-detail response already covers exactly this window
   * (MovieManager.GetDetailAsync), so switching tabs is a client-side filter, no re-fetch. */
  readonly dateTabs: { key: string; date: Date }[] = [];
  selectedDateKey = '';

  /** The showtime the inline booking panel is currently targeting; empty when no panel is open.
   * Clicking a different chip re-targets BookingSelectionComponent instead of navigating away. */
  selectedShowTimeId = '';
  selectedRoomId = '';
  selectedShowTimeLabel = '';
  /** Order carried back via BookingCheckoutComponent's "back to seats" — handed down to the panel
   * once it reopens on the same showtime, then cleared so a later, unrelated showtime pick never
   * replays it. */
  pendingRestore?: BookingCheckoutState;

  /** Name (lower-cased, trimmed) -> photo URL, resolved from the Person directory for the current
   * movie's director(s) and cast. Looked up once per movie load; a cache-miss name just keeps the
   * initials fallback in the template. */
  private _photoByName = new Map<string, string>();
  /** Guards against re-fetching on every movie$ re-emission (e.g. after rating/commenting). */
  private _photoLookupKey = '';

  constructor(
    private _store: Store,
    private _route: ActivatedRoute,
    private _router: Router,
    private _location: Location,
    private _destroyRef: DestroyRef,
    private _cdr: ChangeDetectorRef,
    private _translate: TranslateService,
    private _toast: ToastService,
    private _actions$: Actions,
    private _cinemaSvc: CinemaServiceAgent.HttpService,
    private _dialog: DialogService,
  ) {
    this.movie$ = this._store.select(selectSelectedMovie);
    this.loading$ = this._store.select(selectMoviesLoading);
    this.isAuthenticated$ = this._store.select(selectIsAuthenticated);
  }

  ngOnInit(): void {
    this._routeParam = this._route.snapshot.paramMap.get('slug') ?? '';

    // Must be set up before subscribing to movie$ below: when this movie is already cached in the
    // store (e.g. navigating straight back from checkout), that subscription emits SYNCHRONOUSLY,
    // so _applyRestore's date-tab selection would otherwise get immediately clobbered by these two
    // lines running after it.
    this.dateTabs.push(...this.buildDateTabs());
    this.selectedDateKey = this.dateTabs[0].key;

    // Lowercase up front (backend GUIDs and slugs are both always lowercase) so a differently-cased
    // legacy link still matches movie.id/movie.slug in the filter below and gets healed.
    this._routeParam = this._routeParam.toLowerCase();

    // Provisional until the real movie loads (corrected to movie.id in the subscription below): for
    // a legacy GUID route this is already the real id; for a slug route it lets callers that fire
    // before load (there are none in production — rating/commenting require the loaded movie anyway)
    // still have a stable, non-empty value.
    this.movieId = this._routeParam;

    if (isLegacyMovieId(this._routeParam)) {
      this._store.dispatch(loadMovieDetail({ id: this._routeParam }));
    } else {
      this.detailUrl = movieDetailUrl(this._routeParam);
      this._store.dispatch(loadMovieDetailBySlug({ slug: this._routeParam }));
    }

    const restore = (history.state as { restore?: BookingCheckoutState } | undefined)?.restore;
    let restoreApplied = false;

    this.movie$
      .pipe(
        filter(movie => !!movie && (movie.id === this._routeParam || movie.slug === this._routeParam)),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe(movie => {
        this.movieId = movie.id;
        // Server state wins on every (re)load: own rating/review, and a fresh first comments page.
        this.myScore = movie.myRating ?? 0;
        this.reviewText = movie.myReview ?? '';
        this._detailSeq += 1;
        if (this._commentsMovieId !== movie.id) {
          this._commentsMovieId = movie.id;
          this.extraComments = [];
          this._commentsPage = 1;
        }
        if (this.extraComments.length === 0) {
          this.commentsHasMore = (movie.recentComments?.length ?? 0) >= this._commentPageSize;
        }
        this._syncCanonicalUrl(movie);
        this._loadPhotos(movie);
        if (restore && !restoreApplied) {
          restoreApplied = true;
          this._applyRestore(movie, restore);
        }
      });

    this._actions$.pipe(
      ofType(rateMovieSuccess),
      filter(a => a.movieId === this.movieId),
      takeUntilDestroyed(this._destroyRef),
    ).subscribe(a => {
      this.myScore = a.score;
      this._toast.success(this._translate.instant('movies.detail.ratingSaved'));
      this._cdr.markForCheck();
    });

    this._actions$.pipe(
      ofType(addCommentSuccess),
      filter(a => a.movieId === this.movieId),
      takeUntilDestroyed(this._destroyRef),
    ).subscribe(a => {
      this._patchExtraAfterAdd(a.comment);
      this.newComment = '';
      this.replyText = '';
      this.replyingToId = '';
      this._toast.success(this._translate.instant('movies.detail.commentPosted'));
      this._cdr.markForCheck();
    });

    this._actions$.pipe(
      ofType(updateOwnCommentSuccess),
      filter(a => a.movieId === this.movieId),
      takeUntilDestroyed(this._destroyRef),
    ).subscribe(a => {
      this._mapExtra(c => c.id === a.commentId ? { ...c, content: a.comment?.content ?? c.content } : c);
      this.editingId = '';
      this.editText = '';
      this._toast.success(this._translate.instant('movies.detail.commentUpdated'));
      this._cdr.markForCheck();
    });

    this._actions$.pipe(
      ofType(deleteOwnCommentSuccess),
      filter(a => a.movieId === this.movieId),
      takeUntilDestroyed(this._destroyRef),
    ).subscribe(a => {
      this.extraComments = this.extraComments
        .filter(c => c.id !== a.commentId)
        .map(c => ({ ...c, replies: (c.replies ?? []).filter((r: any) => r.id !== a.commentId) }));
      this._toast.success(this._translate.instant('movies.detail.commentDeleted'));
      this._cdr.markForCheck();
    });

    this._actions$.pipe(
      ofType(rateMovieFailure, addCommentFailure, updateOwnCommentFailure, deleteOwnCommentFailure),
      takeUntilDestroyed(this._destroyRef),
    ).subscribe(() => {
      // The interceptor already showed the translated error toast; just resync eligibility/state.
      this.hoverScore = 0;
      this._store.dispatch(loadMovieDetail({ id: this.movieId }));
      this._cdr.markForCheck();
    });
  }

  /** Keeps the address bar on `/movies/:slug` without re-navigating (which would recreate this
   * component and re-dispatch loadMovieDetail): replaces a legacy GUID, or a differently-cased
   * slug, with the movie's real slug in place. */
  private _syncCanonicalUrl(movie: any): void {
    this.detailUrl = movieDetailUrl(movie.slug);
    if (this._routeParam !== movie.slug) {
      this._routeParam = movie.slug;
      this._location.replaceState(this.detailUrl);
    }
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
    this._openShowTime(st);
    this._cdr.markForCheck();
    setTimeout(() => this._scrollToBookingPanel(), 0);
  }

  private _openShowTime(st: any): void {
    this.selectedShowTimeId = st.id;
    this.selectedRoomId = st.roomId;
    const time = new Date(st.startTime);
    const hh = time.getHours().toString().padStart(2, '0');
    const mm = time.getMinutes().toString().padStart(2, '0');
    const format = screeningFormatLabel(st.roomTypeName, st.projectionForm);
    this.selectedShowTimeLabel = `${st.theaterName} · ${hh}:${mm} · ${format}`;
  }

  /** Reopens the inline panel on the showtime carried back from checkout's "back to seats", on the
   * date tab it falls on, and hands the restored order down so BookingSelectionComponent re-selects
   * (and re-locks) the same seats/tickets/food once that showtime's data loads. A showtime that no
   * longer exists on this movie (e.g. cancelled) is silently ignored — the page just opens normally. */
  private _applyRestore(movie: any, restore: BookingCheckoutState): void {
    const st = (movie.showTimes ?? []).find((s: any) => s.id === restore.showTimeId);
    if (!st) {
      return;
    }
    const dateKey = this.toDateKey(st.startTime);
    if (this.dateTabs.some(t => t.key === dateKey)) {
      this.selectedDateKey = dateKey;
    }
    this._openShowTime(st);
    this.pendingRestore = restore;
    this._cdr.markForCheck();
    setTimeout(() => {
      // Cleared once the embedded panel has had a chance to snapshot it (see BookingSelectionComponent),
      // so selecting a different showtime later never replays this same restore.
      this.pendingRestore = undefined;
      this._scrollToBookingPanel();
    }, 0);
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

  /** Score to paint in the hero widget: the real average once the movie has ratings, else 0 (no
   * ratings yet: no ring, no filled stars). */
  displayScore(movie: any): number {
    return movie.ratingCount > 0 ? movie.averageRating : 0;
  }

  onHeroStarHover(score: number): void {
    this.hoverScore = score;
    this._cdr.markForCheck();
  }

  onHeroStarLeave(): void {
    this.hoverScore = 0;
    this._cdr.markForCheck();
  }

  /** True once the user has a paid, watched (finished-screening) ticket for this movie — only then
   * can they rate or comment. Backend re-checks this regardless; this just drives the UI. */
  canReview(movie: any): boolean {
    return !!movie?.canReview;
  }

  /** Submits immediately on star click — no separate confirm button. RateMovieAsync is an upsert,
   * so re-clicking just updates this user's existing rating rather than adding a duplicate. The
   * rating is only applied locally once the server confirms it (see the rateMovieSuccess handler). */
  rateFromHero(movie: any, score: number): void {
    this.isAuthenticated$.pipe(take(1)).subscribe(isAuthenticated => {
      if (!isAuthenticated) {
        this._router.navigate(['/auth/login'], { queryParams: { returnUrl: this.detailUrl } });
        return;
      }
      if (!this.canReview(movie)) {
        this._toast.info(this._translate.instant('movies.detail.watchFirstToReview'));
        return;
      }
      this._store.dispatch(rateMovie({ movieId: this.movieId, score, review: undefined }));
    });
  }

  commentCount(movie: any): number {
    return movie.totalComments ?? 0;
  }

  /** Top-level comments to render: the first page from the detail plus any "load more" pages
   * (de-duplicated by id: a new comment shifts the page boundary). */
  allComments(movie: any): any[] {
    const first = movie.recentComments ?? [];
    const seen = new Set<string>(first.map((c: any) => c.id));
    return [...first, ...this.extraComments.filter(c => !seen.has(c.id))];
  }

  /** Applies `fn` to every comment and reply held in the "load more" pages. */
  private _mapExtra(fn: (c: any) => any): void {
    this.extraComments = this.extraComments.map(c => ({ ...fn(c), replies: (c.replies ?? []).map((r: any) => fn(r)) }));
  }

  /** A reply to a comment that lives in a "load more" page is appended there; the reloaded first page
   * already carries replies to its own comments. */
  private _patchExtraAfterAdd(comment: any): void {
    if (!comment?.parentId) { return; }
    this.extraComments = this.extraComments.map(c =>
      c.id === comment.parentId ? { ...c, replies: [...(c.replies ?? []), comment] } : c);
  }

  loadMoreComments(): void {
    if (this.commentsLoadingMore) { return; }
    this.commentsLoadingMore = true;
    this._commentsPage += 1;
    const seq = this._detailSeq;
    this._cinemaSvc.getMovieComments(
      CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: this._commentsPage, pageSize: this._commentPageSize }),
      this.movieId,
    ).pipe(takeUntilDestroyed(this._destroyRef)).subscribe({
      next: r => {
        if (seq !== this._detailSeq) {
          // The detail reloaded while this page was in flight; its page boundaries no longer match.
          this._commentsPage -= 1;
          this.commentsLoadingMore = false;
          this._cdr.markForCheck();
          return;
        }
        this.extraComments = [...this.extraComments, ...(r.results ?? [])];
        const loadedTopLevel = this._commentPageSize + this.extraComments.length;
        this.commentsHasMore = loadedTopLevel < (r.totalCount ?? 0);
        this.commentsLoadingMore = false;
        this._cdr.markForCheck();
      },
      error: () => {
        this._commentsPage -= 1;
        this.commentsLoadingMore = false;
        this._cdr.markForCheck();
      },
    });
  }

  /** Saves the star rating together with the review text typed in the review card. */
  saveReview(movie: any): void {
    const score = this.myScore || movie.myRating || 0;
    if (!score) {
      this._toast.info(this._translate.instant('movies.detail.pickScoreFirst'));
      return;
    }
    this._store.dispatch(rateMovie({ movieId: this.movieId, score, review: this.reviewText.trim() }));
  }

  startReply(c: any): void {
    this.replyingToId = c.id;
    this.replyText = '';
    this.editingId = '';
  }

  cancelReply(): void {
    this.replyingToId = '';
    this.replyText = '';
  }

  submitReply(parent: any): void {
    const content = this.replyText.trim();
    if (!content || content.length > this.maxCommentLength) { return; }
    this._store.dispatch(addComment({ movieId: this.movieId, content, parentId: parent.id }));
  }

  startEdit(c: any): void {
    this.editingId = c.id;
    this.editText = c.content;
    this.replyingToId = '';
  }

  cancelEdit(): void {
    this.editingId = '';
    this.editText = '';
  }

  saveEdit(c: any): void {
    const content = this.editText.trim();
    if (!content || content.length > this.maxCommentLength) { return; }
    this._store.dispatch(updateOwnComment({ movieId: this.movieId, commentId: c.id, content }));
  }

  deleteComment(c: any): void {
    this._dialog.openConfirmDialog({
      title: 'movies.detail.delete',
      message: 'movies.detail.deleteCommentConfirm',
      confirmText: 'movies.detail.delete',
      cancelText: 'movies.detail.cancel',
    }).afterClosed().pipe(takeUntilDestroyed(this._destroyRef)).subscribe(confirmed => {
      if (confirmed) {
        this._store.dispatch(deleteOwnComment({ movieId: this.movieId, commentId: c.id }));
      }
    });
  }

  submitComment(movie: any): void {
    const content = this.newComment.trim();
    if (!content) { return; }
    this.isAuthenticated$.pipe(take(1)).subscribe(isAuthenticated => {
      if (!isAuthenticated) {
        this._router.navigate(['/auth/login'], { queryParams: { returnUrl: this.detailUrl } });
        return;
      }
      if (!this.canReview(movie)) {
        this._toast.info(this._translate.instant('movies.detail.watchFirstToReview'));
        return;
      }
      if (content.length > this.maxCommentLength) {
        this._toast.info(this._translate.instant('movies.detail.commentTooLong'));
        return;
      }
      this._store.dispatch(addComment({ movieId: this.movieId, content }));
    });
  }

  private _splitNames(value: string | undefined): string[] {
    return value ? value.split(',').map(s => s.trim()).filter(Boolean) : [];
  }

  getCastMembers(cast: string | undefined): string[] {
    return this._splitNames(cast).slice(0, 6);
  }

  getDirectors(director: string | undefined): string[] {
    return this._splitNames(director);
  }

  getInitials(name: string): string {
    return name.split(' ').filter(Boolean).map(w => w[0]).join('').toUpperCase().slice(0, 2);
  }

  /** Photo URL for a Cast & Crew tile name, resolved from the Person directory; undefined falls
   * back to the initials tile in the template. */
  photoFor(name: string): string | undefined {
    return this._photoByName.get(name.trim().toLocaleLowerCase());
  }

  /** True when the movie has explicit MovieCredit rows — the Cast & Crew section then renders those
   * (with real linked photos and character names) instead of splitting Director/Cast strings. */
  hasCredits(movie: any): boolean {
    return (movie?.credits?.length ?? 0) > 0;
  }

  creditDirectors(movie: any): any[] {
    return (movie?.credits ?? [])
      .filter((c: any) => c.creditType === CinemaServiceAgent.CreditType.Director)
      .sort((a: any, b: any) => a.displayOrder - b.displayOrder);
  }

  creditActors(movie: any): any[] {
    return (movie?.credits ?? [])
      .filter((c: any) => c.creditType === CinemaServiceAgent.CreditType.Actor)
      .sort((a: any, b: any) => a.displayOrder - b.displayOrder);
  }

  /** Batch-resolves photos for this movie's director(s) + cast in a single request, once per
   * distinct name set — later movie$ re-emissions (rating/comment reloads) don't re-fetch. Skipped
   * entirely when the movie has explicit MovieCredit rows, since those already carry their photo. */
  private _loadPhotos(movie: any): void {
    if (this.hasCredits(movie)) {
      this._photoByName = new Map<string, string>();
      this._photoLookupKey = 'credits:' + movie.id;
      return;
    }

    const names = [...this.getDirectors(movie.director), ...this.getCastMembers(movie.cast)];
    const key = names.join('|').toLocaleLowerCase();
    if (key === this._photoLookupKey) {
      return;
    }
    this._photoLookupKey = key;

    if (names.length === 0) {
      this._photoByName = new Map<string, string>();
      return;
    }

    this._cinemaSvc.getPeopleByNames(names)
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe({
        next: people => {
          const map = new Map<string, string>();
          for (const p of people) {
            if (p.name && p.photoUrl) {
              map.set(p.name.trim().toLocaleLowerCase(), p.photoUrl);
            }
          }
          this._photoByName = map;
          this._cdr.markForCheck();
        },
        // Lookup failure just keeps the initials fallback — no toast, not worth interrupting the page.
        error: () => {},
      });
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

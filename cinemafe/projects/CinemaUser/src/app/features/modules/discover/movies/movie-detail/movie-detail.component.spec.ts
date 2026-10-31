import { NEVER, of, throwError } from 'rxjs';
import { selectIsAuthenticated, CinemaServiceAgent } from 'CinemaLib';
import { MovieDetailComponent } from './movie-detail.component';

const showTime = (over: Partial<any>): any => ({
  id: 'st-1',
  roomId: 'room-1',
  startTime: '2026-01-01T20:00:00',
  theaterId: 'theater-1',
  theaterName: 'Cinema One',
  theaterAddress: '1 Main St',
  roomName: 'Room A',
  roomTypeName: 'Standard',
  projectionForm: 0,
  availableSeats: 50,
  ...over,
});

describe('MovieDetailComponent', () => {
  beforeEach(() => {
    selectedMovie = null;
    routeParam = 'movie-1';
  });


  let router: { navigate: ReturnType<typeof vi.fn> };
  let store: { select: (selector: unknown) => unknown; dispatch: ReturnType<typeof vi.fn> };
  let toast: { success: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn>; info: ReturnType<typeof vi.fn> };
  let isAuthenticated = true;
  let selectedMovie: unknown = null;
  let location: { replaceState: ReturnType<typeof vi.fn> };
  let routeParam = 'movie-1';
  let cinemaSvc: { getPeopleByNames: ReturnType<typeof vi.fn> };

  const build = () => {
    store = {
      select: (selector: unknown) => {
        if (selector === selectIsAuthenticated) { return of(isAuthenticated); }
        return of(selectedMovie);
      },
      dispatch: vi.fn(),
    };
    const route = { snapshot: { paramMap: { get: (key: string) => key === 'slug' ? routeParam : null } } };
    router = { navigate: vi.fn() };
    location = { replaceState: vi.fn() };
    const destroyRef = { onDestroy: () => () => {} };
    const cdr = { markForCheck: vi.fn() };
    const translate = { instant: (key: string) => key };
    toast = { success: vi.fn(), error: vi.fn(), info: vi.fn() };
    const actions$ = NEVER;
    cinemaSvc = { getPeopleByNames: vi.fn(() => of([])) };
    return new MovieDetailComponent(
      store as never, route as never, router as never, location as never, destroyRef as never, cdr as never, translate as never, toast as never,
      actions$ as never, cinemaSvc as never,
    );
  };

  describe('buildDateTabs', () => {
    it('returns 4 consecutive local dates starting today', () => {
      const c = build();
      c.ngOnInit();

      expect(c.dateTabs).toHaveLength(4);
      const today = new Date();
      for (let i = 0; i < 4; i++) {
        const expected = new Date(today);
        expected.setDate(expected.getDate() + i);
        expect(c.dateTabs[i].date.toDateString()).toBe(expected.toDateString());
      }
      expect(c.selectedDateKey).toBe(c.dateTabs[0].key);
    });
  });

  describe('groupByTheater', () => {
    it('returns only the selected date, one entry per theater, sorted by name, formats intact', () => {
      const c = build();
      c.ngOnInit();
      const today = c.dateTabs[0].key;
      const tomorrow = c.dateTabs[1].key;
      const todayIso = `${today}T20:00:00`;
      const showTimes = [
        showTime({ id: 's1', theaterId: 'zeta-id', theaterName: 'Zeta Cinema', theaterAddress: '9 Zeta Rd', startTime: `${today}T18:00:00` }),
        showTime({ id: 's2', theaterId: 'alpha-id', theaterName: 'Alpha Cinema', theaterAddress: '1 Alpha St', startTime: `${today}T21:00:00` }),
        showTime({ id: 's3', theaterId: 'alpha-id', theaterName: 'Alpha Cinema', theaterAddress: '1 Alpha St', startTime: todayIso }),
        showTime({ id: 's4', theaterId: 'alpha-id', theaterName: 'Alpha Cinema', theaterAddress: '1 Alpha St', startTime: `${tomorrow}T10:00:00` }),
      ];

      const groups = c.groupByTheater(showTimes, today);

      expect(groups.map(g => g.theaterName)).toEqual(['Alpha Cinema', 'Zeta Cinema']);
      expect(groups.map(g => g.theaterAddress)).toEqual(['1 Alpha St', '9 Zeta Rd']);
      const alpha = groups.find(g => g.theaterName === 'Alpha Cinema')!;
      const times = alpha.formats.flatMap(f => f.items.map((i: any) => i.id));
      expect(times).toEqual(['s3', 's2']); // ascending by time, tomorrow's s4 excluded
    });

    it('keeps two distinct theaters that share a display name as separate groups', () => {
      const c = build();
      c.ngOnInit();
      const today = c.dateTabs[0].key;
      const showTimes = [
        showTime({ id: 's1', theaterId: 'branch-a', theaterName: 'Cinema Plaza', theaterAddress: '1 Branch A', startTime: `${today}T18:00:00` }),
        showTime({ id: 's2', theaterId: 'branch-b', theaterName: 'Cinema Plaza', theaterAddress: '2 Branch B', startTime: `${today}T19:00:00` }),
      ];

      const groups = c.groupByTheater(showTimes, today);

      expect(groups).toHaveLength(2);
      expect(groups.map(g => g.theaterAddress).sort()).toEqual(['1 Branch A', '2 Branch B']);
    });

    it('defaults theaterAddress to an empty string when missing', () => {
      const c = build();
      c.ngOnInit();
      const today = c.dateTabs[0].key;
      const showTimes = [showTime({ id: 's1', theaterAddress: undefined, startTime: `${today}T18:00:00` })];

      const groups = c.groupByTheater(showTimes, today);

      expect(groups[0].theaterAddress).toBe('');
    });

    it('assigns a 23:30 local showtime to its own local day, not the next day', () => {
      const c = build();
      c.ngOnInit();
      const today = c.dateTabs[0].key;
      const tomorrow = c.dateTabs[1].key;
      const showTimes = [showTime({ id: 'late', theaterName: 'Cinema One', startTime: `${today}T23:30:00` })];

      expect(c.countForDate(showTimes, today)).toBe(1);
      expect(c.countForDate(showTimes, tomorrow)).toBe(0);
      expect(c.groupByTheater(showTimes, tomorrow)).toEqual([]);
    });

    it('returns an empty array for a date with no showtimes', () => {
      const c = build();
      c.ngOnInit();
      const tomorrow = c.dateTabs[1].key;
      const showTimes = [showTime({ startTime: `${c.dateTabs[0].key}T20:00:00` })];

      expect(c.countForDate(showTimes, tomorrow)).toBe(0);
      expect(c.groupByTheater(showTimes, tomorrow)).toEqual([]);
    });
  });

  describe('selectShowTime', () => {
    beforeEach(() => { isAuthenticated = true; });

    it('opens the inline panel for an authenticated visitor', () => {
      const c = build();
      c.ngOnInit();
      const st = showTime({ id: 'st-9', roomId: 'room-9', theaterName: 'Cinema One', startTime: '2026-01-01T19:30:00' });

      c.selectShowTime(st);

      expect(c.selectedShowTimeId).toBe('st-9');
      expect(c.selectedRoomId).toBe('room-9');
      expect(c.selectedShowTimeLabel).toContain('Cinema One');
      expect(c.selectedShowTimeLabel).toContain('19:30');
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it('opens the inline panel for an anonymous visitor too — login is deferred to checkout', () => {
      isAuthenticated = false;
      const c = build();
      c.ngOnInit();

      c.selectShowTime(showTime({ id: 'st-9' }));

      expect(c.selectedShowTimeId).toBe('st-9');
      expect(router.navigate).not.toHaveBeenCalled();
    });
  });

  describe('closeBookingPanel / selectDate', () => {
    it('closeBookingPanel clears the selected showtime', () => {
      const c = build();
      c.ngOnInit();
      c.selectShowTime(showTime({ id: 'st-9' }));

      c.closeBookingPanel();

      expect(c.selectedShowTimeId).toBe('');
      expect(c.selectedRoomId).toBe('');
      expect(c.selectedShowTimeLabel).toBe('');
    });

    it('selectDate closes any open panel', () => {
      const c = build();
      c.ngOnInit();
      c.selectShowTime(showTime({ id: 'st-9' }));

      c.selectDate(c.dateTabs[1].key);

      expect(c.selectedShowTimeId).toBe('');
    });
  });

  describe('rateFromHero / submitComment eligibility gating', () => {
    beforeEach(() => { isAuthenticated = true; });

    it('rateFromHero dispatches rateMovie when the user can review', () => {
      const c = build();
      c.ngOnInit();

      c.rateFromHero({ canReview: true }, 8);

      expect(store.dispatch).toHaveBeenCalledWith(expect.objectContaining({ movieId: 'movie-1', score: 8 }));
      expect(toast.info).not.toHaveBeenCalled();
    });

    it('rateFromHero shows a hint and does not dispatch when the user has not watched the movie', () => {
      const c = build();
      c.ngOnInit();
      store.dispatch.mockClear();

      c.rateFromHero({ canReview: false }, 8);

      expect(store.dispatch).not.toHaveBeenCalled();
      expect(toast.info).toHaveBeenCalledWith('movies.detail.watchFirstToReview');
    });

    it('submitComment dispatches addComment when the user can review', () => {
      const c = build();
      c.ngOnInit();
      c.newComment = 'Great movie';

      c.submitComment({ canReview: true });

      expect(store.dispatch).toHaveBeenCalledWith(expect.objectContaining({ movieId: 'movie-1', content: 'Great movie' }));
    });

    it('submitComment shows a hint and does not dispatch when the user has not watched the movie', () => {
      const c = build();
      c.ngOnInit();
      store.dispatch.mockClear();
      c.newComment = 'Great movie';

      c.submitComment({ canReview: false });

      expect(store.dispatch).not.toHaveBeenCalled();
      expect(toast.info).toHaveBeenCalledWith('movies.detail.watchFirstToReview');
    });

    it('redirects to login before checking eligibility when anonymous', () => {
      isAuthenticated = false;
      const c = build();
      c.ngOnInit();
      store.dispatch.mockClear();

      c.rateFromHero({ canReview: false }, 8);

      expect(router.navigate).toHaveBeenCalledWith(['/auth/login'], { queryParams: { returnUrl: '/movies/movie-1' } });
      expect(store.dispatch).not.toHaveBeenCalled();
      expect(toast.info).not.toHaveBeenCalled();
    });
  });

  describe('route param resolution (slug vs. legacy GUID)', () => {
    it('a slug param dispatches loadMovieDetailBySlug', () => {
      routeParam = 'wicked';
      const c = build();

      c.ngOnInit();

      expect(store.dispatch).toHaveBeenCalledWith(expect.objectContaining({ slug: 'wicked' }));
    });

    it('a GUID param dispatches loadMovieDetail by id and, once the movie loads, rewrites the URL to its slug', () => {
      routeParam = '0383e9ff-d765-4f42-9421-8d79586ac766';
      const c = build();

      c.ngOnInit();
      expect(store.dispatch).toHaveBeenCalledWith(expect.objectContaining({ id: '0383e9ff-d765-4f42-9421-8d79586ac766' }));

      selectedMovie = { id: '0383e9ff-d765-4f42-9421-8d79586ac766', slug: 'wicked', showTimes: [] };
      const c2 = build();
      c2.ngOnInit();

      expect(c2.movieId).toBe('0383e9ff-d765-4f42-9421-8d79586ac766');
      expect(location.replaceState).toHaveBeenCalledWith('/movies/wicked');
    });

    it('a slug param that already matches the loaded movie does not rewrite the URL', () => {
      routeParam = 'wicked';
      selectedMovie = { id: 'movie-id-1', slug: 'wicked', showTimes: [] };
      const c = build();

      c.ngOnInit();

      expect(location.replaceState).not.toHaveBeenCalled();
      expect(c.movieId).toBe('movie-id-1');
    });
  });

  describe('Cast & Crew photo lookup', () => {
    it('requests photos once for director + cast names on movie load', () => {
      selectedMovie = { id: 'movie-1', slug: 'movie-1', director: 'Christopher Nolan', cast: 'A, B', showTimes: [] };
      const c = build();

      c.ngOnInit();

      expect(cinemaSvc.getPeopleByNames).toHaveBeenCalledTimes(1);
      expect(cinemaSvc.getPeopleByNames).toHaveBeenCalledWith(['Christopher Nolan', 'A', 'B']);
    });

    it('maps a matched name (case-insensitively) to its photo URL, leaving unmatched names falsy', () => {
      selectedMovie = { id: 'movie-1', slug: 'movie-1', director: 'Christopher Nolan', cast: 'A, B', showTimes: [] };
      cinemaSvc = { getPeopleByNames: vi.fn(() => of([{ name: 'A', photoUrl: 'photo-a.jpg' }])) };
      const c = new MovieDetailComponent(
        store as never, { snapshot: { paramMap: { get: () => 'movie-1' } } } as never, router as never, location as never,
        { onDestroy: () => () => {} } as never, { markForCheck: vi.fn() } as never, { instant: (k: string) => k } as never, toast as never,
        NEVER as never, cinemaSvc as never,
      );

      c.ngOnInit();

      expect(c.photoFor('A')).toBe('photo-a.jpg');
      expect(c.photoFor('a')).toBe('photo-a.jpg');
      expect(c.photoFor('B')).toBeFalsy();
    });

    it('does not re-request photos on a second emission of the same movie', () => {
      selectedMovie = { id: 'movie-1', slug: 'movie-1', director: 'Christopher Nolan', cast: 'A, B', showTimes: [] };
      const c = build();

      c.ngOnInit();
      c.ngOnInit();

      expect(cinemaSvc.getPeopleByNames).toHaveBeenCalledTimes(1);
    });

    it('sends no request when the movie has neither director nor cast', () => {
      selectedMovie = { id: 'movie-1', slug: 'movie-1', showTimes: [] };
      const c = build();

      c.ngOnInit();

      expect(cinemaSvc.getPeopleByNames).not.toHaveBeenCalled();
    });

    it('leaves the photo map empty and does not throw when the lookup request fails', () => {
      selectedMovie = { id: 'movie-1', slug: 'movie-1', director: 'Christopher Nolan', showTimes: [] };
      cinemaSvc = { getPeopleByNames: vi.fn(() => throwError(() => new Error('network error'))) };
      const c = new MovieDetailComponent(
        store as never, { snapshot: { paramMap: { get: () => 'movie-1' } } } as never, router as never, location as never,
        { onDestroy: () => () => {} } as never, { markForCheck: vi.fn() } as never, { instant: (k: string) => k } as never, toast as never,
        NEVER as never, cinemaSvc as never,
      );

      expect(() => c.ngOnInit()).not.toThrow();
      expect(c.photoFor('Christopher Nolan')).toBeFalsy();
    });
  });

  describe('Cast & Crew explicit credits', () => {
    it('uses MovieCredit rows and does not call getPeopleByNames when credits are present', () => {
      selectedMovie = {
        id: 'movie-1', slug: 'movie-1', director: 'Christopher Nolan', cast: 'A, B', showTimes: [],
        credits: [
          { personId: 'p2', personName: 'Emily Blunt', personPhotoUrl: 'eb.jpg', creditType: CinemaServiceAgent.CreditType.Actor, characterName: 'Kitty Oppenheimer', displayOrder: 1 },
          { personId: 'p1', personName: 'Christopher Nolan', personPhotoUrl: 'cn.jpg', creditType: CinemaServiceAgent.CreditType.Director, characterName: null, displayOrder: 0 },
        ],
      };
      const c = build();

      c.ngOnInit();

      expect(cinemaSvc.getPeopleByNames).not.toHaveBeenCalled();
      expect(c.hasCredits(selectedMovie)).toBe(true);
      expect(c.creditDirectors(selectedMovie).map((x: any) => x.personName)).toEqual(['Christopher Nolan']);
      expect(c.creditActors(selectedMovie).map((x: any) => x.personName)).toEqual(['Emily Blunt']);
    });

    it('sorts directors and actors by displayOrder', () => {
      selectedMovie = {
        id: 'movie-1', slug: 'movie-1', showTimes: [],
        credits: [
          { personId: 'p2', personName: 'Second Actor', creditType: CinemaServiceAgent.CreditType.Actor, displayOrder: 1 },
          { personId: 'p1', personName: 'First Actor', creditType: CinemaServiceAgent.CreditType.Actor, displayOrder: 0 },
        ],
      };
      const c = build();

      expect(c.creditActors(selectedMovie).map((x: any) => x.personName)).toEqual(['First Actor', 'Second Actor']);
    });

    it('falls back to string-splitting + getPeopleByNames when credits is an empty array', () => {
      selectedMovie = { id: 'movie-1', slug: 'movie-1', director: 'Christopher Nolan', cast: 'A, B', showTimes: [], credits: [] };
      const c = build();

      c.ngOnInit();

      expect(cinemaSvc.getPeopleByNames).toHaveBeenCalledTimes(1);
      expect(cinemaSvc.getPeopleByNames).toHaveBeenCalledWith(['Christopher Nolan', 'A', 'B']);
    });

    it('hasCredits is false for undefined or empty credits', () => {
      const c = build();

      expect(c.hasCredits({ credits: undefined })).toBe(false);
      expect(c.hasCredits({ credits: [] })).toBe(false);
      expect(c.hasCredits({ credits: [{ creditType: CinemaServiceAgent.CreditType.Actor }] })).toBe(true);
    });
  });

  describe('external ratings (rtTier)', () => {
    it('classifies a Rotten Tomatoes score as fresh at or above 60%', () => {
      const c = build();

      expect(c.rtTier('93%')).toBe('fresh');
      expect(c.rtTier('60%')).toBe('fresh');
    });

    it('classifies a Rotten Tomatoes score as rotten below 60%', () => {
      const c = build();

      expect(c.rtTier('59%')).toBe('rotten');
    });

    it('returns null for missing or unparsable scores', () => {
      const c = build();

      expect(c.rtTier(undefined)).toBeNull();
      expect(c.rtTier(null)).toBeNull();
      expect(c.rtTier('N/A')).toBeNull();
    });
  });
});

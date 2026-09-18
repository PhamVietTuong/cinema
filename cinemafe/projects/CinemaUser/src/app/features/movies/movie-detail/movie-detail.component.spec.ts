import { of } from 'rxjs';
import { selectIsAuthenticated } from 'CinemaLib';
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
  let router: { navigate: ReturnType<typeof vi.fn> };
  let isAuthenticated = true;

  const build = () => {
    const store = { select: (selector: unknown) => selector === selectIsAuthenticated ? of(isAuthenticated) : of(null), dispatch: vi.fn() };
    const route = { snapshot: { paramMap: { get: () => 'movie-1' } } };
    router = { navigate: vi.fn() };
    const cdr = { markForCheck: vi.fn() };
    const translate = { instant: (key: string) => key };
    const toast = { success: vi.fn(), error: vi.fn(), info: vi.fn() };
    return new MovieDetailComponent(store as never, route as never, router as never, cdr as never, translate as never, toast as never);
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

    it('redirects an anonymous visitor to login instead of opening the panel', () => {
      isAuthenticated = false;
      const c = build();
      c.ngOnInit();

      c.selectShowTime(showTime({ id: 'st-9' }));

      expect(c.selectedShowTimeId).toBe('');
      expect(router.navigate).toHaveBeenCalledWith(['/auth/login'], { queryParams: { returnUrl: '/movies/movie-1' } });
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
});

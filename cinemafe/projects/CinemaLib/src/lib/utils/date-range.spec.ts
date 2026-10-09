import {
  MAX_RANGE_DAYS, addDays, combineDateAndTime, isRangeWithin, parseDateKey, startOfWeek, toDateKey, toTimeKey, toWallClockUtc, weekDays,
} from './date-range';

describe('date-range helpers', () => {
  it('startOfWeek returns the Monday of the week, also for a Sunday', () => {
    expect(toDateKey(startOfWeek(new Date(2026, 9, 7)))).toBe('2026-10-05'); // Wednesday
    expect(toDateKey(startOfWeek(new Date(2026, 9, 11)))).toBe('2026-10-05'); // Sunday
    expect(toDateKey(startOfWeek(new Date(2026, 9, 5)))).toBe('2026-10-05'); // Monday
  });

  it('weekDays lists seven consecutive days', () => {
    const days = weekDays(new Date(2026, 9, 5));
    expect(days.length).toBe(7);
    expect(toDateKey(days[6])).toBe('2026-10-11');
  });

  it('addDays crosses month boundaries', () => {
    expect(toDateKey(addDays(new Date(2026, 0, 31), 1))).toBe('2026-02-01');
  });

  it('parseDateKey rejects malformed and impossible dates', () => {
    expect(parseDateKey('2026-02-30')).toBeNull();
    expect(parseDateKey('abc')).toBeNull();
    expect(parseDateKey('')).toBeNull();
    expect(toDateKey(parseDateKey('2026-02-28')!)).toBe('2026-02-28');
  });

  it('toWallClockUtc keeps the local wall clock in the ISO string', () => {
    const local = new Date(2026, 9, 4, 19, 30);
    expect(toWallClockUtc(local).toISOString()).toBe('2026-10-04T19:30:00.000Z');
  });

  it('isRangeWithin enforces a positive span of at most the cap', () => {
    const from = new Date(2026, 9, 1);
    expect(isRangeWithin(from, addDays(from, MAX_RANGE_DAYS))).toBe(true);
    expect(isRangeWithin(from, addDays(from, MAX_RANGE_DAYS + 1))).toBe(false);
    expect(isRangeWithin(from, from)).toBe(false);
    expect(isRangeWithin(addDays(from, 2), from)).toBe(false);
  });

  it('combineDateAndTime builds a local date and rejects bad input', () => {
    const date = combineDateAndTime('2026-10-04', '09:05')!;
    expect(toTimeKey(date)).toBe('09:05');
    expect(toDateKey(date)).toBe('2026-10-04');
    expect(combineDateAndTime('2026-10-04', '25:00')).toBeNull();
    expect(combineDateAndTime('nope', '09:00')).toBeNull();
  });
});

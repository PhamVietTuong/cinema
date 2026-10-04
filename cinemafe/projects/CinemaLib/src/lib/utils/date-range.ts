/**
 * Calendar helpers shared by the staff scheduling screens (roster, time sheet, schedule board).
 *
 * The cinema API stores wall-clock times (SQL `datetime`, no zone) and the generated clients serialise a
 * `Date` with `toISOString()`, which would shift a local midnight to the previous day in UTC+7.
 * `toWallClockUtc` builds a Date whose ISO string reads exactly like the local wall clock, so the server
 * receives the time the user picked.
 */

/** Longest span the roster and time-sheet endpoints accept, in days. */
export const MAX_RANGE_DAYS = 31;

const MS_PER_DAY = 24 * 60 * 60 * 1000;

/** Local midnight of the given date (a copy). */
export function startOfDay(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}

/** The date `days` calendar days later (negative goes back); keeps the local time of day. */
export function addDays(date: Date, days: number): Date {
  const result = new Date(date.getTime());
  result.setDate(result.getDate() + days);
  return result;
}

/** Monday 00:00 (local) of the week containing `date`. */
export function startOfWeek(date: Date): Date {
  const day = startOfDay(date);
  const offset = (day.getDay() + 6) % 7;
  return addDays(day, -offset);
}

/** The seven days of the week starting at `weekStart`. */
export function weekDays(weekStart: Date): Date[] {
  const days: Date[] = [];
  for (let i = 0; i < 7; i++) {
    days.push(addDays(weekStart, i));
  }
  return days;
}

/** Local `yyyy-MM-dd` key of a date (what `<input type="date">` and the paged filters use). */
export function toDateKey(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

/** Parses a `yyyy-MM-dd` key into local midnight; null when it is not a valid date. */
export function parseDateKey(key: string | null | undefined): Date | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec((key ?? '').trim());
  if (!match) {
    return null;
  }
  const year = Number(match[1]);
  const month = Number(match[2]) - 1;
  const day = Number(match[3]);
  const date = new Date(year, month, day);
  if (date.getFullYear() !== year || date.getMonth() !== month || date.getDate() !== day) {
    return null;
  }
  return date;
}

/** A Date whose `toISOString()` reproduces the local wall-clock time of `date` (see the file header). */
export function toWallClockUtc(date: Date): Date {
  return new Date(date.getTime() - date.getTimezoneOffset() * 60000);
}

/** Whole days between two dates, fractions included. */
export function daysBetween(from: Date, to: Date): number {
  return (to.getTime() - from.getTime()) / MS_PER_DAY;
}

/** True when `to` is after `from` and the span is at most `maxDays` days. */
export function isRangeWithin(from: Date, to: Date, maxDays: number = MAX_RANGE_DAYS): boolean {
  const span = daysBetween(from, to);
  return span > 0 && span <= maxDays;
}

/** Local `HH:mm` of a date (what `<input type="time">` uses). */
export function toTimeKey(date: Date): string {
  const hours = String(date.getHours()).padStart(2, '0');
  const minutes = String(date.getMinutes()).padStart(2, '0');
  return `${hours}:${minutes}`;
}

/** Combines a `yyyy-MM-dd` key and an `HH:mm` time into a local Date; null when either is invalid. */
export function combineDateAndTime(dateKey: string, time: string): Date | null {
  const date = parseDateKey(dateKey);
  const match = /^(\d{2}):(\d{2})$/.exec((time ?? '').trim());
  if (!date || !match) {
    return null;
  }
  const hours = Number(match[1]);
  const minutes = Number(match[2]);
  if (hours > 23 || minutes > 59) {
    return null;
  }
  date.setHours(hours, minutes, 0, 0);
  return date;
}

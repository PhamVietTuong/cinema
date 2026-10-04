import { addDays, combineDateAndTime, toDateKey } from 'CinemaLib';

export interface ShiftSlot {
  start: Date;
  end: Date;
}

/**
 * Builds a shift's start and end from the day it begins and the two clock times. An end at or before the start
 * means the shift runs overnight and ends the next day. Null when a time is invalid.
 */
export function buildShiftSlot(dateKey: string, startTime: string, endTime: string): ShiftSlot | null {
  const start = combineDateAndTime(dateKey, startTime);
  let end = combineDateAndTime(dateKey, endTime);
  if (!start || !end) {
    return null;
  }
  if (end.getTime() <= start.getTime()) {
    end = addDays(end, 1);
  }
  return { start, end };
}

/** Shifts of one staff member that begin on `day` (by local start date), ordered by start time. */
export function shiftsOnDay<T extends { userId?: string; startTime?: Date }>(shifts: readonly T[], userId: string, day: Date): T[] {
  const key = toDateKey(day);
  return shifts
    .filter(shift => shift.userId === userId && !!shift.startTime && toDateKey(shift.startTime) === key)
    .sort((a, b) => a.startTime!.getTime() - b.startTime!.getTime());
}

/** Total scheduled hours of a list of shifts, rounded to one decimal. */
export function totalShiftHours(shifts: readonly { startTime?: Date; endTime?: Date }[]): number {
  let ms = 0;
  for (const shift of shifts) {
    if (shift.startTime && shift.endTime) {
      ms += shift.endTime.getTime() - shift.startTime.getTime();
    }
  }
  return Math.round((ms / 3600000) * 10) / 10;
}

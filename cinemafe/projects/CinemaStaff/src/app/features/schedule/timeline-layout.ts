/** Pure layout math for the schedule board's per-room timelines (percentages of the visible window). */

const HOUR_MS = 60 * 60 * 1000;
/** Window shown when a day has fewer showtimes than this range covers. */
const DEFAULT_START_HOUR = 8;
const DEFAULT_END_HOUR = 24;

export interface TimelineShowTime {
  start: Date;
  end: Date;
  bufferEnd: Date;
}

/** Visible time span of the board, in epoch milliseconds (always whole hours). */
export interface TimelineWindow {
  startMs: number;
  endMs: number;
}

export interface TimelineBlock {
  /** Left edge of the showtime, 0..100. */
  leftPct: number;
  /** Width of the showtime itself. */
  widthPct: number;
  /** Left edge of the turnover buffer (the showtime's end). */
  bufferLeftPct: number;
  /** Width of the turnover buffer after the showtime. */
  bufferWidthPct: number;
}

export interface TimelineTick {
  leftPct: number;
  /** `HH:00` label. */
  label: string;
}

/**
 * The visible window for a day: 08:00 to 24:00 of `day`, widened to whole hours when a showtime starts earlier
 * or its buffer ends later (late-night shows run past midnight).
 */
export function computeWindow(showTimes: readonly TimelineShowTime[], day: Date): TimelineWindow {
  const midnight = new Date(day.getFullYear(), day.getMonth(), day.getDate()).getTime();
  let startMs = midnight + DEFAULT_START_HOUR * HOUR_MS;
  let endMs = midnight + DEFAULT_END_HOUR * HOUR_MS;
  for (const showTime of showTimes) {
    startMs = Math.min(startMs, Math.floor(showTime.start.getTime() / HOUR_MS) * HOUR_MS);
    endMs = Math.max(endMs, Math.ceil(showTime.bufferEnd.getTime() / HOUR_MS) * HOUR_MS);
  }
  return { startMs, endMs };
}

function clampPct(value: number): number {
  return Math.min(100, Math.max(0, value));
}

/** Position and size of one showtime and its turnover buffer inside `window`. */
export function layoutShowTime(showTime: TimelineShowTime, window: TimelineWindow): TimelineBlock {
  const span = window.endMs - window.startMs;
  const left = clampPct(((showTime.start.getTime() - window.startMs) / span) * 100);
  const end = clampPct(((showTime.end.getTime() - window.startMs) / span) * 100);
  const bufferEnd = clampPct(((showTime.bufferEnd.getTime() - window.startMs) / span) * 100);
  return {
    leftPct: left,
    widthPct: Math.max(0, end - left),
    bufferLeftPct: end,
    bufferWidthPct: Math.max(0, bufferEnd - end),
  };
}

/** One tick per whole hour of the window, for the grid header. */
export function hourTicks(window: TimelineWindow): TimelineTick[] {
  const span = window.endMs - window.startMs;
  const ticks: TimelineTick[] = [];
  for (let ms = window.startMs; ms < window.endMs; ms += HOUR_MS) {
    const hour = new Date(ms).getHours();
    ticks.push({
      leftPct: ((ms - window.startMs) / span) * 100,
      label: `${String(hour).padStart(2, '0')}:00`,
    });
  }
  return ticks;
}

/** Share of seats sold, 0..100 (0 for a room with no capacity). */
export function soldPercent(sold: number | undefined, capacity: number | undefined): number {
  if (!capacity || capacity <= 0) {
    return 0;
  }
  return clampPct(Math.round(((sold ?? 0) / capacity) * 100));
}

import { CinemaServiceAgent, DayOfWeekValues, MultiplierBand, multiplierBand } from 'CinemaLib';

type TimeSlotDTO = CinemaServiceAgent.TimeSlotDTO;
type TicketPriceDTO = CinemaServiceAgent.TicketPriceDTO;
type HolidayDTO = CinemaServiceAgent.HolidayDTO;
type DayOfWeek = CinemaServiceAgent.DayOfWeek;

/** One time-slot segment drawn on a weekday's timeline bar, positioned as a [0,100] percentage of the
 * 00:00–24:00 ruler. */
export interface TimelineSegmentVm {
  timeSlot: TimeSlotDTO;
  leftPct: number;
  widthPct: number;
  multiplier: number;
  rowId?: string;
  isDefault: boolean;
  band: Exclude<MultiplierBand, 'holiday'>;
}

/** One weekday row: either its segmented timeline, or (when a holiday falls on it within the display
 * window) a full-width holiday override banner instead. */
export interface WeekRowVm {
  day: DayOfWeek;
  labelKey: string;
  segments: TimelineSegmentVm[];
  holiday?: HolidayDTO;
  holidayDate?: string;
}

const MINUTES_PER_DAY = 24 * 60;

/** Parses "HH:mm" into minutes since midnight, or null if malformed. */
function parseMinutes(hhmm: string | undefined): number | null {
  if (!hhmm) {
    return null;
  }
  const match = /^(\d{1,2}):(\d{2})$/.exec(hhmm.trim());
  if (!match) {
    return null;
  }
  const hours = Number(match[1]);
  const mins = Number(match[2]);
  if (hours > 23 || mins > 59) {
    return null;
  }
  return hours * 60 + mins;
}

/** `yyyy-MM-dd` built from local date parts — never `toISOString()`, which can roll to the wrong
 * calendar day across a timezone offset. Used for "today" on the client. */
export function todayIsoLocal(): string {
  const now = new Date();
  const y = now.getFullYear();
  const m = String(now.getMonth() + 1).padStart(2, '0');
  const d = String(now.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

/** `yyyy-MM-dd` for a NSwag-parsed DateOnly value: these parse as UTC midnight, so the UTC slice is the
 * true calendar date (unlike `todayIsoLocal`, which is for a genuinely local "today"). */
export function holidayIso(h: HolidayDTO): string {
  return (h.date as unknown as Date).toISOString().slice(0, 10);
}

function dayOfWeekOfIso(iso: string): DayOfWeek {
  // Date-only string parses as UTC midnight; getUTCDay avoids a local-timezone off-by-one.
  return new Date(`${iso}T00:00:00Z`).getUTCDay() as DayOfWeek;
}

/**
 * Builds the week's timeline rows (Mon→Sun) from the theater's time slots, the current room type's
 * ticket-price rows, and upcoming holidays. A holiday attaches to a weekday row only when its date
 * falls within the rolling 7-day window starting `todayIso` AND its weekday matches that row — so a
 * holiday more than a week out shows only in the holidays sidebar, not as a banner yet.
 */
export function buildWeekRows(
  timeSlots: TimeSlotDTO[],
  prices: TicketPriceDTO[],
  holidays: HolidayDTO[],
  todayIso: string,
): { rows: WeekRowVm[]; invalidSlots: TimeSlotDTO[] } {
  const validSlots: { slot: TimeSlotDTO; start: number; end: number }[] = [];
  const invalidSlots: TimeSlotDTO[] = [];
  for (const slot of timeSlots) {
    const start = parseMinutes(slot.startTime);
    const end = parseMinutes(slot.endTime);
    if (start === null || end === null || end <= start) {
      invalidSlots.push(slot);
      continue;
    }
    validSlots.push({ slot, start, end });
  }
  validSlots.sort((a, b) => a.start - b.start);

  const todayStart = new Date(`${todayIso}T00:00:00Z`).getTime();
  const windowHolidaysByDay = new Map<DayOfWeek, { holiday: HolidayDTO; iso: string }>();
  for (const h of holidays) {
    const iso = holidayIso(h);
    const offsetDays = Math.round((new Date(`${iso}T00:00:00Z`).getTime() - todayStart) / 86_400_000);
    if (offsetDays < 0 || offsetDays > 6) {
      continue;
    }
    const day = dayOfWeekOfIso(iso);
    // If more than one holiday lands on the same weekday within the window, the soonest one wins.
    const existing = windowHolidaysByDay.get(day);
    if (!existing || iso < existing.iso) {
      windowHolidaysByDay.set(day, { holiday: h, iso });
    }
  }

  const rows: WeekRowVm[] = DayOfWeekValues.map(d => {
    const segments: TimelineSegmentVm[] = validSlots.map(({ slot, start, end }) => {
      const row = prices.find(p => p.timeSlotId === slot.id && p.dayOfWeek === d.value);
      const multiplier = row?.priceMultiplier ?? 1;
      return {
        timeSlot: slot,
        leftPct: (start / MINUTES_PER_DAY) * 100,
        widthPct: ((end - start) / MINUTES_PER_DAY) * 100,
        multiplier,
        rowId: row?.id,
        isDefault: !row,
        band: multiplierBand(multiplier),
      };
    });

    const windowHoliday = windowHolidaysByDay.get(d.value);
    return {
      day: d.value,
      labelKey: d.name,
      segments,
      holiday: windowHoliday?.holiday,
      holidayDate: windowHoliday?.iso,
    };
  });

  return { rows, invalidSlots };
}

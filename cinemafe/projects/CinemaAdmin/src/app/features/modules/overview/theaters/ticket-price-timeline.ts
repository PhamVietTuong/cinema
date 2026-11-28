import { CinemaServiceAgent, DayOfWeekValues, MultiplierBand, multiplierBand, toDateKey } from 'CinemaLib';

type TimeSlotDTO = CinemaServiceAgent.TimeSlotDTO;
type TicketPriceDTO = CinemaServiceAgent.TicketPriceDTO;
type HolidayDTO = CinemaServiceAgent.HolidayDTO;
type HolidayTimeSlotPriceDTO = CinemaServiceAgent.HolidayTimeSlotPriceDTO;
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

/** A holiday's factor for one time slot: its own override row when it has one, else the holiday default. */
export interface HolidaySegmentVm {
  timeSlot: TimeSlotDTO;
  leftPct: number;
  widthPct: number;
  multiplier: number;
  /** Id of the HolidayTimeSlotPrice row, when this slot has its own override. */
  rowId?: string;
  isDefault: boolean;
  band: Exclude<MultiplierBand, 'holiday'>;
}

/** One weekday row: its segmented timeline, or (when a holiday falls on it within the display window)
 * the holiday's own segmented timeline instead — one segment per time slot, each editable. */
export interface WeekRowVm {
  day: DayOfWeek;
  labelKey: string;
  segments: TimelineSegmentVm[];
  holiday?: HolidayDTO;
  holidayDate?: string;
  holidaySegments: HolidaySegmentVm[];
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
  return toDateKey(new Date());
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

interface ValidSlot { slot: TimeSlotDTO; start: number; end: number }

/** The holiday's factor for each valid slot: its own override row when it has one, else the default. */
function holidaySegmentsFor(
  validSlots: ValidSlot[],
  holiday: HolidayDTO,
  holidayPrices: HolidayTimeSlotPriceDTO[],
): HolidaySegmentVm[] {
  return validSlots.map(({ slot, start, end }) => {
    const row = holidayPrices.find(p => p.holidayId === holiday.id && p.timeSlotId === slot.id);
    const multiplier = row?.priceMultiplier ?? holiday.priceMultiplier ?? 1;
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
}

/** The theater's non-wrapping slots (start < end) in time order. */
function validSlotsOf(timeSlots: TimeSlotDTO[]): ValidSlot[] {
  const valid: ValidSlot[] = [];
  for (const slot of timeSlots) {
    const start = parseMinutes(slot.startTime);
    const end = parseMinutes(slot.endTime);
    if (start !== null && end !== null && end > start) {
      valid.push({ slot, start, end });
    }
  }
  return valid.sort((a, b) => a.start - b.start);
}

/** One segment per valid slot for a single holiday, used by the holidays sidebar (any date, not only
 * the rolling 7-day window the weekday rows cover). */
export function holidaySlotSegments(
  timeSlots: TimeSlotDTO[],
  holiday: HolidayDTO,
  holidayPrices: HolidayTimeSlotPriceDTO[],
): HolidaySegmentVm[] {
  return holidaySegmentsFor(validSlotsOf(timeSlots), holiday, holidayPrices);
}

/**
 * For a late-night slot: the holiday's factor on the slot immediately before it (its own override, else
 * the holiday default). Null when the slot is not late night or has no preceding slot, so the dialog
 * never warns for it.
 */
export function precedingHolidayMultiplier(
  timeSlots: TimeSlotDTO[],
  holiday: HolidayDTO,
  holidayPrices: HolidayTimeSlotPriceDTO[],
  slot: TimeSlotDTO,
): number | null {
  if (!isLateNightSlot(slot)) {
    return null;
  }
  const segments = holidaySlotSegments(timeSlots, holiday, holidayPrices);
  const start = parseMinutes(slot.startTime)!;
  let preceding: HolidaySegmentVm | undefined;
  for (const seg of segments) {
    const segStart = parseMinutes(seg.timeSlot.startTime)!;
    if (seg.timeSlot.id !== slot.id && segStart < start) {
      preceding = seg;
    }
  }
  return preceding?.multiplier ?? null;
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
  holidayPrices: HolidayTimeSlotPriceDTO[] = [],
): { rows: WeekRowVm[]; invalidSlots: TimeSlotDTO[] } {
  const validSlots = validSlotsOf(timeSlots);
  const invalidSlots = timeSlots.filter(s => !validSlots.some(v => v.slot === s));

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
    const holidaySegments = windowHoliday ? holidaySegmentsFor(validSlots, windowHoliday.holiday, holidayPrices) : [];
    return {
      day: d.value,
      labelKey: d.name,
      segments,
      holiday: windowHoliday?.holiday,
      holidayDate: windowHoliday?.iso,
      holidaySegments,
    };
  });

  return { rows, invalidSlots };
}

/** A slot starting at or after this time of day (22:00) counts as late night. */
const LATE_NIGHT_START_MINUTES = 22 * 60;

/** True when the slot starts at 22:00 or later. */
export function isLateNightSlot(slot: TimeSlotDTO): boolean {
  const start = parseMinutes(slot.startTime);
  return start !== null && start >= LATE_NIGHT_START_MINUTES;
}

/**
 * For a late-night slot: the multiplier in force on each weekday for the slot immediately before it
 * (default 1 when that slot has no price row, or when the late-night slot is the first of the day).
 * Returns null when the slot is not late night, so the dialog never warns for it.
 */
export function precedingMultipliers(
  timeSlots: TimeSlotDTO[],
  prices: TicketPriceDTO[],
  slot: TimeSlotDTO,
): Record<number, number> | null {
  if (!isLateNightSlot(slot)) {
    return null;
  }
  const start = parseMinutes(slot.startTime)!;
  let preceding: TimeSlotDTO | undefined;
  let precedingStart = -1;
  for (const candidate of timeSlots) {
    const candidateStart = parseMinutes(candidate.startTime);
    const candidateEnd = parseMinutes(candidate.endTime);
    if (candidateStart === null || candidateEnd === null || candidateEnd <= candidateStart || candidate.id === slot.id) {
      continue;
    }
    if (candidateStart < start && candidateStart > precedingStart) {
      preceding = candidate;
      precedingStart = candidateStart;
    }
  }
  const result: Record<number, number> = {};
  for (const d of DayOfWeekValues) {
    const row = preceding ? prices.find(p => p.timeSlotId === preceding!.id && p.dayOfWeek === d.value) : undefined;
    result[d.value] = row?.priceMultiplier ?? 1;
  }
  return result;
}

/** True when the multiplier is above the preceding slot's multiplier on any of the selected days. */
export function exceedsPrecedingSlot(
  multiplier: number,
  days: DayOfWeek[],
  preceding: Record<number, number> | null | undefined,
): boolean {
  if (!preceding || !Number.isFinite(multiplier)) {
    return false;
  }
  return days.some(day => multiplier > (preceding[day] ?? 1) + 1e-9);
}

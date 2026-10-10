import { CinemaServiceAgent } from 'CinemaLib';
import { buildWeekRows, holidayIso, todayIsoLocal } from './ticket-price-timeline';

type TimeSlotDTO = CinemaServiceAgent.TimeSlotDTO;
type TicketPriceDTO = CinemaServiceAgent.TicketPriceDTO;
type HolidayDTO = CinemaServiceAgent.HolidayDTO;

const ROOM_TYPE_ID = 'rt-1';
const THEATER_ID = 'th-1';

function slot(id: string, name: string, startTime: string, endTime: string): TimeSlotDTO {
  return CinemaServiceAgent.TimeSlotDTO.fromJS({ id, theaterId: THEATER_ID, name, startTime, endTime });
}

function price(timeSlotId: string, dayOfWeek: CinemaServiceAgent.DayOfWeek, priceMultiplier: number): TicketPriceDTO {
  return CinemaServiceAgent.TicketPriceDTO.fromJS({
    id: `${timeSlotId}-${dayOfWeek}`, theaterId: THEATER_ID, roomTypeId: ROOM_TYPE_ID, timeSlotId, dayOfWeek, priceMultiplier,
  });
}

function holiday(name: string, dateIso: string, priceMultiplier: number): HolidayDTO {
  return CinemaServiceAgent.HolidayDTO.fromJS({ id: `h-${dateIso}`, name, date: dateIso, priceMultiplier });
}

describe('todayIsoLocal', () => {
  it('returns a yyyy-MM-dd string for the current local date', () => {
    expect(todayIsoLocal()).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  });
});

describe('holidayIso', () => {
  it('reads the UTC calendar date of a parsed DateOnly value', () => {
    expect(holidayIso(holiday('Sample', '2026-10-13', 1.5))).toBe('2026-10-13');
  });
});

describe('buildWeekRows', () => {
  const morning = slot('ts-morning', 'Sáng', '08:00', '12:00');
  const afternoon = slot('ts-afternoon', 'Chiều', '12:00', '18:00');

  it('orders rows Monday through Sunday', () => {
    const { rows } = buildWeekRows([morning], [], [], '2026-10-06');
    expect(rows.map(r => r.day)).toEqual([
      CinemaServiceAgent.DayOfWeek.Monday,
      CinemaServiceAgent.DayOfWeek.Tuesday,
      CinemaServiceAgent.DayOfWeek.Wednesday,
      CinemaServiceAgent.DayOfWeek.Thursday,
      CinemaServiceAgent.DayOfWeek.Friday,
      CinemaServiceAgent.DayOfWeek.Saturday,
      CinemaServiceAgent.DayOfWeek.Sunday,
    ]);
  });

  it('positions segments as a percentage of the 24h ruler', () => {
    const { rows } = buildWeekRows([morning, afternoon], [], [], '2026-10-06');
    const monday = rows.find(r => r.day === CinemaServiceAgent.DayOfWeek.Monday)!;
    expect(monday.segments[0].leftPct).toBeCloseTo((8 * 60 / 1440) * 100, 5);
    expect(monday.segments[0].widthPct).toBeCloseTo((4 * 60 / 1440) * 100, 5);
    expect(monday.segments[1].leftPct).toBeCloseTo((12 * 60 / 1440) * 100, 5);
  });

  it('defaults a cell with no matching row to multiplier 1 and isDefault true', () => {
    const { rows } = buildWeekRows([morning], [], [], '2026-10-06');
    const monday = rows.find(r => r.day === CinemaServiceAgent.DayOfWeek.Monday)!;
    expect(monday.segments[0].multiplier).toBe(1);
    expect(monday.segments[0].isDefault).toBe(true);
  });

  it('applies a matching day-of-week row and clears isDefault', () => {
    const rows1 = buildWeekRows([morning], [price('ts-morning', CinemaServiceAgent.DayOfWeek.Monday, 1.3)], [], '2026-10-06').rows;
    const monday = rows1.find(r => r.day === CinemaServiceAgent.DayOfWeek.Monday)!;
    expect(monday.segments[0].multiplier).toBe(1.3);
    expect(monday.segments[0].isDefault).toBe(false);
    expect(monday.segments[0].band).toBe('peak');
  });

  it('excludes a slot that wraps past midnight and reports it as invalid', () => {
    const wrapping = slot('ts-late', 'Khuya', '22:00', '02:00');
    const { rows, invalidSlots } = buildWeekRows([morning, wrapping], [], [], '2026-10-06');
    expect(invalidSlots).toEqual([wrapping]);
    expect(rows[0].segments).toHaveLength(1);
  });

  it('attaches a holiday to its weekday row when within the 7-day window', () => {
    // 2026-10-06 is a Monday; 2026-10-11 (Sunday) is 5 days out, inside the window.
    const h = holiday('Sample Holiday', '2026-10-11', 1.8);
    const { rows } = buildWeekRows([morning], [], [h], '2026-10-06');
    const sunday = rows.find(r => r.day === CinemaServiceAgent.DayOfWeek.Sunday)!;
    expect(sunday.holiday).toBe(h);
    expect(sunday.holidayDate).toBe('2026-10-11');
    const monday = rows.find(r => r.day === CinemaServiceAgent.DayOfWeek.Monday)!;
    expect(monday.holiday).toBeUndefined();
  });

  it('leaves a holiday outside the 7-day window unattached to any row', () => {
    const h = holiday('Far Holiday', '2026-10-20', 1.8);
    const { rows } = buildWeekRows([morning], [], [h], '2026-10-06');
    expect(rows.every(r => r.holiday === undefined)).toBe(true);
  });
});

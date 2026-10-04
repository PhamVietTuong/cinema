import { buildShiftSlot, shiftsOnDay, totalShiftHours } from './shift-times';

describe('shift times', () => {
  it('builds a same-day shift', () => {
    const slot = buildShiftSlot('2026-10-05', '09:00', '17:30')!;
    expect(slot.start.getHours()).toBe(9);
    expect(slot.end.getDate()).toBe(5);
    expect(slot.end.getMinutes()).toBe(30);
  });

  it('rolls an end at or before the start over to the next day', () => {
    const slot = buildShiftSlot('2026-10-05', '22:00', '06:00')!;
    expect(slot.end.getDate()).toBe(6);
    expect(buildShiftSlot('2026-10-05', '09:00', '09:00')!.end.getDate()).toBe(6);
  });

  it('returns null for invalid input', () => {
    expect(buildShiftSlot('2026-10-05', '', '10:00')).toBeNull();
    expect(buildShiftSlot('bad', '09:00', '10:00')).toBeNull();
  });

  it('filters shifts by user and start day, ordered by start', () => {
    const shifts = [
      { userId: 'a', startTime: new Date(2026, 9, 5, 14) },
      { userId: 'a', startTime: new Date(2026, 9, 5, 8) },
      { userId: 'a', startTime: new Date(2026, 9, 6, 8) },
      { userId: 'b', startTime: new Date(2026, 9, 5, 8) },
    ];
    const result = shiftsOnDay(shifts, 'a', new Date(2026, 9, 5));
    expect(result.map(s => s.startTime.getHours())).toEqual([8, 14]);
  });

  it('totals scheduled hours', () => {
    expect(totalShiftHours([
      { startTime: new Date(2026, 9, 5, 8), endTime: new Date(2026, 9, 5, 12, 30) },
      { startTime: new Date(2026, 9, 6, 8), endTime: new Date(2026, 9, 6, 10) },
    ])).toBe(6.5);
  });
});

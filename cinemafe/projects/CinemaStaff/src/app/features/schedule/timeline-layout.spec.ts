import { computeWindow, hourTicks, layoutShowTime, soldPercent } from './timeline-layout';

const day = new Date(2026, 9, 4);
const at = (hour: number, minute = 0): Date => new Date(2026, 9, 4, hour, minute);

describe('timeline layout', () => {
  it('defaults to 08:00-24:00 for an empty day', () => {
    const window = computeWindow([], day);
    expect(new Date(window.startMs).getHours()).toBe(8);
    expect((window.endMs - window.startMs) / 3600000).toBe(16);
  });

  it('widens the window to whole hours for early and late showtimes', () => {
    const window = computeWindow([
      { start: at(7, 30), end: at(9, 30), bufferEnd: at(9, 45) },
      { start: new Date(2026, 9, 4, 23, 0), end: new Date(2026, 9, 5, 1, 10), bufferEnd: new Date(2026, 9, 5, 1, 25) },
    ], day);
    expect(new Date(window.startMs).getHours()).toBe(7);
    expect(window.endMs).toBe(new Date(2026, 9, 5, 2, 0).getTime());
  });

  it('places a showtime and its buffer as percentages of the window', () => {
    const window = { startMs: at(8).getTime(), endMs: at(24).getTime() }; // 16 hours
    const block = layoutShowTime({ start: at(12), end: at(14), bufferEnd: at(14, 30) }, window);
    expect(block.leftPct).toBeCloseTo(25, 5);
    expect(block.widthPct).toBeCloseTo(12.5, 5);
    expect(block.bufferLeftPct).toBeCloseTo(37.5, 5);
    expect(block.bufferWidthPct).toBeCloseTo(3.125, 5);
  });

  it('clamps blocks that fall outside the window', () => {
    const window = { startMs: at(8).getTime(), endMs: at(24).getTime() };
    const block = layoutShowTime({ start: at(6), end: at(9), bufferEnd: at(9, 15) }, window);
    expect(block.leftPct).toBe(0);
    expect(block.widthPct).toBeCloseTo(6.25, 5);
  });

  it('emits one tick per hour', () => {
    const ticks = hourTicks({ startMs: at(8).getTime(), endMs: at(11).getTime() });
    expect(ticks.map(t => t.label)).toEqual(['08:00', '09:00', '10:00']);
    expect(ticks[1].leftPct).toBeCloseTo(33.3333, 3);
  });

  it('computes the sold share safely', () => {
    expect(soldPercent(50, 200)).toBe(25);
    expect(soldPercent(5, 0)).toBe(0);
    expect(soldPercent(undefined, 10)).toBe(0);
    expect(soldPercent(300, 200)).toBe(100);
  });
});

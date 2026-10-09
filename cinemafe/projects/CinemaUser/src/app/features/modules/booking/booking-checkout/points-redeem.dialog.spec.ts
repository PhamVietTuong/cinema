import { PointsRedeemDialog } from './points-redeem.dialog';

describe('PointsRedeemDialog hold-to-repeat', () => {
  let close: ReturnType<typeof vi.fn>;
  const event = { preventDefault: vi.fn() } as unknown as PointerEvent;

  const build = (max: number, value: number) => {
    close = vi.fn();
    return new PointsRedeemDialog({ close } as never, { balance: max, max, value, pointValue: 1000 });
  };

  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('steps once on a quick press', () => {
    const d = build(50, 10);

    d.startHold(1, event);
    d.stopHold();
    vi.advanceTimersByTime(2000);

    expect(d.points).toBe(11);
  });

  it('keeps counting up while held and stops at the maximum', () => {
    const d = build(50, 10);

    d.startHold(1, event);
    vi.advanceTimersByTime(5000);

    expect(d.points).toBe(50);
    d.stopHold();
  });

  it('keeps counting down while held and stops at zero', () => {
    const d = build(50, 30);

    d.startHold(-1, event);
    vi.advanceTimersByTime(5000);

    expect(d.points).toBe(0);
    d.stopHold();
  });

  it('stops counting once released', () => {
    const d = build(50, 10);

    d.startHold(1, event);
    vi.advanceTimersByTime(600);
    d.stopHold();
    const atRelease = d.points;
    vi.advanceTimersByTime(2000);

    expect(d.points).toBe(atRelease);
    expect(atRelease).toBeGreaterThan(11);
  });
});

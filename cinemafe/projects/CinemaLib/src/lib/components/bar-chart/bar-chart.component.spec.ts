import { barPercent } from './bar-chart.component';

describe('barPercent', () => {
  it('scales a value against the max', () => {
    expect(barPercent(50, 200)).toBe(25);
  });

  it('caps at 100 and floors at 0', () => {
    expect(barPercent(300, 200)).toBe(100);
    expect(barPercent(-5, 200)).toBe(0);
  });

  it('is 0 when the max is not positive', () => {
    expect(barPercent(10, 0)).toBe(0);
  });
});

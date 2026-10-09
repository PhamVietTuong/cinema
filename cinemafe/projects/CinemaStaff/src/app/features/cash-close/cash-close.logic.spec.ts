import {
  drawerVariance,
  fromDateInputValue,
  isValidCount,
  needsReconciliation,
  toDateInputValue,
  varianceTone,
} from './cash-close.logic';

describe('cash close logic', () => {
  it('computes variance as counted minus expected', () => {
    expect(drawerVariance(500000, 490000)).toBe(-10000);
    expect(drawerVariance(500000, 510000)).toBe(10000);
    expect(drawerVariance(500000, 500000)).toBe(0);
  });

  it('has no variance until a count is typed', () => {
    expect(drawerVariance(500000, null)).toBeNull();
    expect(drawerVariance(500000, undefined)).toBeNull();
    expect(drawerVariance(500000, NaN)).toBeNull();
  });

  it('classifies the variance tone', () => {
    expect(varianceTone(0)).toBe('balanced');
    expect(varianceTone(null)).toBe('balanced');
    expect(varianceTone(-1)).toBe('short');
    expect(varianceTone(1)).toBe('over');
  });

  it('accepts only non-negative finite counts', () => {
    expect(isValidCount(0)).toBe(true);
    expect(isValidCount(125000)).toBe(true);
    expect(isValidCount(-1)).toBe(false);
    expect(isValidCount(null)).toBe(false);
    expect(isValidCount(Infinity)).toBe(false);
  });

  it('previews reconciliation against the tolerance', () => {
    expect(needsReconciliation(-10000)).toBe(true);
    expect(needsReconciliation(0)).toBe(false);
    expect(needsReconciliation(null)).toBe(false);
    expect(needsReconciliation(-500, 1000)).toBe(false);
    expect(needsReconciliation(1500, 1000)).toBe(true);
  });

  it('round-trips a date input value through noon so the date part survives UTC serialisation', () => {
    const parsed = fromDateInputValue('2026-10-04');
    expect(parsed).toBeDefined();
    expect(parsed!.getHours()).toBe(12);
    expect(toDateInputValue(parsed!)).toBe('2026-10-04');
    expect(parsed!.toISOString().startsWith('2026-10-04')).toBe(true);
    expect(fromDateInputValue('')).toBeUndefined();
    expect(fromDateInputValue('04/10/2026')).toBeUndefined();
  });
});

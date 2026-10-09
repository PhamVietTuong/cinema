import { fromServerUtc, serverUtcMs } from './server-time';

describe('fromServerUtc', () => {
  it('treats a zoneless string as UTC', () => {
    expect(fromServerUtc('2026-10-04T12:22:00')?.toISOString()).toBe('2026-10-04T12:22:00.000Z');
  });

  it('keeps a string that already has a zone', () => {
    expect(fromServerUtc('2026-10-04T12:22:00Z')?.toISOString()).toBe('2026-10-04T12:22:00.000Z');
    expect(fromServerUtc('2026-10-04T19:22:00+07:00')?.toISOString()).toBe('2026-10-04T12:22:00.000Z');
  });

  it('re-reads a locally parsed Date as UTC fields', () => {
    const parsedAsLocal = new Date(2026, 9, 4, 12, 22, 5, 7);
    const fixed = fromServerUtc(parsedAsLocal)!;
    expect(fixed.toISOString()).toBe('2026-10-04T12:22:05.007Z');
  });

  it('returns undefined for missing or invalid input', () => {
    expect(fromServerUtc(null)).toBeUndefined();
    expect(fromServerUtc(undefined)).toBeUndefined();
    expect(fromServerUtc('')).toBeUndefined();
    expect(fromServerUtc('nope')).toBeUndefined();
    expect(fromServerUtc(new Date(NaN))).toBeUndefined();
  });
});

describe('serverUtcMs', () => {
  it('returns epoch ms or NaN', () => {
    expect(serverUtcMs('1970-01-01T00:00:01')).toBe(1000);
    expect(serverUtcMs(undefined)).toBeNaN();
  });
});

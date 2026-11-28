import { formatVnd } from './currency';

describe('formatVnd', () => {
  it('formats whole dong with thousands separators and the đ suffix', () => {
    expect(formatVnd(1234567)).toBe('1,234,567đ');
    expect(formatVnd(0)).toBe('0đ');
  });

  it('rounds away any fraction', () => {
    expect(formatVnd(99999.6)).toBe('100,000đ');
  });

  it('accepts numeric strings', () => {
    expect(formatVnd('45000')).toBe('45,000đ');
  });

  it('renders empty for missing or non-numeric input', () => {
    expect(formatVnd(null)).toBe('');
    expect(formatVnd(undefined)).toBe('');
    expect(formatVnd('')).toBe('');
    expect(formatVnd('abc')).toBe('');
  });

  it('follows the given locale', () => {
    expect(formatVnd(1234567, 'en-US')).toContain('1,234,567');
  });
});

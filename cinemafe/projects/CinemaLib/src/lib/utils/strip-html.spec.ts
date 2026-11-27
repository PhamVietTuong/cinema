import { stripHtml } from './strip-html';

describe('stripHtml', () => {
  it('returns empty for null/undefined', () => {
    expect(stripHtml(null)).toBe('');
    expect(stripHtml(undefined)).toBe('');
  });

  it('keeps plain text unchanged', () => {
    expect(stripHtml('Một bộ phim hay')).toBe('Một bộ phim hay');
  });

  it('strips tags and separates blocks with a space', () => {
    expect(stripHtml('<p>Dòng <b>một</b></p><p>Dòng hai</p>')).toBe('Dòng một Dòng hai');
  });
});

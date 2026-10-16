import { slugify } from './slugify';

describe('slugify', () => {
  it('lowercases and dashes a plain title', () => {
    expect(slugify('Avengers: Endgame')).toBe('avengers-endgame');
  });

  it('strips Vietnamese diacritics, including đ/Đ', () => {
    expect(slugify('Lật Mặt 7: Một Điều Ước')).toBe('lat-mat-7-mot-dieu-uoc');
    expect(slugify('Đất Rừng Phương Nam')).toBe('dat-rung-phuong-nam');
  });

  it('collapses punctuation and surrounding whitespace into single dashes', () => {
    expect(slugify('  --Spider-Man   2!! ')).toBe('spider-man-2');
    expect(slugify('Fast & Furious 10')).toBe('fast-furious-10');
  });

  it('returns an empty string for null, undefined, blank or symbols-only input', () => {
    expect(slugify(null)).toBe('');
    expect(slugify(undefined)).toBe('');
    expect(slugify('')).toBe('');
    expect(slugify('   ')).toBe('');
    expect(slugify('!!!')).toBe('');
  });

  it('returns an empty string when nothing survives (e.g. non-Latin script)', () => {
    expect(slugify('千と千尋')).toBe('');
  });
});

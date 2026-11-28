import { BREAKPOINT_PHONE, BREAKPOINT_TABLET, BREAKPOINT_TABLET_SMALL, MEDIA_PHONE, MEDIA_TABLET, MEDIA_TABLET_SMALL } from './breakpoints';

describe('breakpoints', () => {
  it('mirrors the scss mixin boundaries (480 / 768 / 1024)', () => {
    expect([BREAKPOINT_PHONE, BREAKPOINT_TABLET_SMALL, BREAKPOINT_TABLET]).toEqual([480, 768, 1024]);
  });

  it('builds max-width media queries', () => {
    expect(MEDIA_PHONE).toBe('(max-width: 480px)');
    expect(MEDIA_TABLET_SMALL).toBe('(max-width: 768px)');
    expect(MEDIA_TABLET).toBe('(max-width: 1024px)');
  });
});

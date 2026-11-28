/**
 * Responsive breakpoints for TypeScript (BreakpointObserver, matchMedia). The numbers mirror `$bp-*` in
 * `theme/_mixins.scss` — change them in both places. Styles must use the `phone` / `tablet-small` / `tablet` mixins,
 * never a hand-typed media query.
 */
export const BREAKPOINT_PHONE = 480;
export const BREAKPOINT_TABLET_SMALL = 768;
export const BREAKPOINT_TABLET = 1024;

/** Media queries equal to the `phone`, `tablet-small` and `tablet` mixins. */
export const MEDIA_PHONE = `(max-width: ${BREAKPOINT_PHONE}px)`;
export const MEDIA_TABLET_SMALL = `(max-width: ${BREAKPOINT_TABLET_SMALL}px)`;
export const MEDIA_TABLET = `(max-width: ${BREAKPOINT_TABLET}px)`;

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** True for a legacy `/movies/:id` route param (a GUID) rather than a friendly slug. */
export function isLegacyMovieId(value: string): boolean {
  return GUID_PATTERN.test(value);
}

/** Router commands for a movie's detail page, keyed by its (backend-authoritative) slug. */
export function movieDetailCommands(slug: string): string[] {
  return ['/movies', slug];
}

/** Same URL as `movieDetailCommands`, as a path string (for `returnUrl` and `Location.replaceState`). */
export function movieDetailUrl(slug: string): string {
  return movieDetailCommands(slug).join('/');
}

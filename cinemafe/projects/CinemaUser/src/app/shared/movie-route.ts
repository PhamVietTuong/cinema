import { slugify } from 'CinemaLib';

/** Router commands for a movie's detail page; appends a cosmetic slug when the title yields one. */
export function movieDetailCommands(id: string, title?: string | null): string[] {
  const slug = slugify(title);
  if (!slug) {
    return ['/movies', id];
  }
  return ['/movies', id, slug];
}

/** Same URL as `movieDetailCommands`, as a path string (for `returnUrl` and `Location.replaceState`). */
export function movieDetailUrl(id: string, title?: string | null): string {
  return movieDetailCommands(id, title).join('/');
}

/**
 * Turns a movie title into a URL-friendly slug for display purposes (e.g. in `/movies/:id/:slug`).
 * Vietnamese `đ`/`Đ` are handled explicitly because Unicode NFD does not decompose them into a base
 * letter plus a combining mark the way accented Latin letters do.
 */
export function slugify(value: string | null | undefined): string {
  const input = (value ?? '').trim();
  if (!input) {
    return '';
  }

  const withoutDiacritics = input
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D');

  return withoutDiacritics
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}

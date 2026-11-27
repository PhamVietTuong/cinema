/** Reduces an HTML fragment (e.g. a rich-text movie description) to plain text, collapsing whitespace. */
export function stripHtml(html: string | null | undefined): string {
  if (!html) {
    return '';
  }
  const withBreaks = html.replace(/<\s*(br|\/p|\/div|\/li)\s*\/?>/gi, ' ');
  const doc = new DOMParser().parseFromString(withBreaks, 'text/html');
  return (doc.body.textContent ?? '').replace(/\s+/g, ' ').trim();
}

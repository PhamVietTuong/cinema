/**
 * Turns a stored trailer link into a safe, embeddable YouTube URL. The only `trailerUrl` shape
 * seeded today is `youtube.com/watch?v=<id>`, but `youtu.be`, `/embed/` and `/shorts/` links resolve
 * too so the dialog keeps working if that changes. Anything else (or a malformed id) returns null and
 * the caller falls back to an external link — never pass raw user/API input straight into an iframe `src`.
 */

const YOUTUBE_ID_PATTERN = /^[A-Za-z0-9_-]{11}$/;

function extractYouTubeId(url: URL): string | null {
  const host = url.hostname.replace(/^www\./, '');
  if (host === 'youtu.be') {
    const id = url.pathname.slice(1);
    return YOUTUBE_ID_PATTERN.test(id) ? id : null;
  }
  if (host === 'youtube.com' || host === 'm.youtube.com' || host === 'music.youtube.com') {
    if (url.pathname === '/watch') {
      const id = url.searchParams.get('v') ?? '';
      return YOUTUBE_ID_PATTERN.test(id) ? id : null;
    }
    const embedMatch = /^\/(?:embed|shorts)\/([^/]+)$/.exec(url.pathname);
    if (embedMatch && YOUTUBE_ID_PATTERN.test(embedMatch[1])) {
      return embedMatch[1];
    }
  }
  return null;
}

/** The `youtube-nocookie.com/embed/<id>` URL for a trailer link, or null when it isn't a recognised YouTube URL. */
export function toYouTubeEmbedUrl(trailerUrl: string | null | undefined): string | null {
  if (!trailerUrl) {
    return null;
  }
  let parsed: URL;
  try {
    parsed = new URL(trailerUrl);
  } catch {
    return null;
  }
  if (parsed.protocol !== 'https:' && parsed.protocol !== 'http:') {
    return null;
  }
  const id = extractYouTubeId(parsed);
  if (!id) {
    return null;
  }
  return `https://www.youtube-nocookie.com/embed/${id}?autoplay=1&rel=0`;
}

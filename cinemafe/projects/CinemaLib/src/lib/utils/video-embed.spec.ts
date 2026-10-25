import { toYouTubeEmbedUrl } from './video-embed';

describe('toYouTubeEmbedUrl', () => {
  it('resolves a watch URL', () => {
    expect(toYouTubeEmbedUrl('https://www.youtube.com/watch?v=dQw4w9WgXcQ'))
      .toBe('https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ?autoplay=1&rel=0');
  });

  it('ignores extra query params on a watch URL', () => {
    expect(toYouTubeEmbedUrl('https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=30s'))
      .toBe('https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ?autoplay=1&rel=0');
  });

  it('resolves a youtu.be short link', () => {
    expect(toYouTubeEmbedUrl('https://youtu.be/dQw4w9WgXcQ'))
      .toBe('https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ?autoplay=1&rel=0');
  });

  it('resolves an existing embed URL', () => {
    expect(toYouTubeEmbedUrl('https://www.youtube.com/embed/dQw4w9WgXcQ'))
      .toBe('https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ?autoplay=1&rel=0');
  });

  it('resolves a shorts URL', () => {
    expect(toYouTubeEmbedUrl('https://www.youtube.com/shorts/dQw4w9WgXcQ'))
      .toBe('https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ?autoplay=1&rel=0');
  });

  it('returns null for a non-YouTube URL', () => {
    expect(toYouTubeEmbedUrl('https://example.com/video.mp4')).toBeNull();
  });

  it('returns null for a malformed video id', () => {
    expect(toYouTubeEmbedUrl('https://www.youtube.com/watch?v=short')).toBeNull();
  });

  it('returns null for empty, undefined or null input', () => {
    expect(toYouTubeEmbedUrl('')).toBeNull();
    expect(toYouTubeEmbedUrl(undefined)).toBeNull();
    expect(toYouTubeEmbedUrl(null)).toBeNull();
  });

  it('returns null for a javascript: URL', () => {
    expect(toYouTubeEmbedUrl('javascript:alert(1)')).toBeNull();
  });

  it('returns null for an unparseable string', () => {
    expect(toYouTubeEmbedUrl('not a url')).toBeNull();
  });
});

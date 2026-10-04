import { matchApiError } from './api-error-catalog';

/**
 * Resolves an `apiErrors` key (with interpolation params) to text in the current language, or null
 * when there is no translation. Registered once at app start from TranslateService.
 */
export type ApiErrorTranslator = (key: string, params: Record<string, string>) => string | null;

let translator: ApiErrorTranslator | null = null;

/** Called by provideCinemaTranslation(); pass null to go back to showing the server text untouched. */
export function registerApiErrorTranslator(next: ApiErrorTranslator | null): void {
  translator = next;
}

/**
 * Translates a message the API sent (always English) into the UI language. A message that is not in
 * the catalog, or any message while no translator is registered, is returned unchanged.
 */
export function translateApiError(message: string): string {
  if (!translator) {
    return message;
  }
  const match = matchApiError(message);
  if (!match) {
    return message;
  }
  return translator(match.key, match.params) ?? message;
}

/**
 * Extracts the message the API meant a human to read, in the current UI language.
 *
 * ExceptionMiddleware answers a rejected request with `{ error, statusCode }`, and the generated
 * NSwag clients wrap that in an ApiException whose own `message` is the generic
 * "An unexpected server error occurred." — the useful text sits unparsed in `response`. Reading
 * `err.error` (the plain HttpErrorResponse shape) therefore misses it and silently falls back.
 *
 * Both shapes are handled so this works whether the call went through a generated client or
 * HttpClient directly. The caller's `fallback` is returned as given: it is already localized.
 */
export function apiErrorMessage(err: unknown, fallback: string): string {
  const raw = rawApiErrorMessage(err);
  return raw === null ? fallback : translateApiError(raw);
}

/** The server's own (English) message, untranslated, or null when the payload carries none. Use it to branch on content. */
export function rawApiErrorMessage(err: unknown): string | null {
  const e = err as { response?: unknown; error?: unknown } | null | undefined;

  // NSwag ApiException: the raw body, usually JSON, occasionally a bare string.
  if (typeof e?.response === 'string' && e.response) {
    try {
      const parsed = JSON.parse(e.response);
      const message = parsed?.error ?? parsed?.message;
      if (typeof message === 'string' && message) { return message; }
    } catch {
      return e.response;
    }
  }

  // Plain HttpErrorResponse: Angular has already parsed the body onto `error`.
  const body = e?.error as { error?: unknown; message?: unknown } | string | undefined;
  if (typeof body === 'string' && body) { return body; }
  const message = (body as { error?: unknown; message?: unknown })?.error ?? (body as { message?: unknown })?.message;
  if (typeof message === 'string' && message) { return message; }

  return null;
}

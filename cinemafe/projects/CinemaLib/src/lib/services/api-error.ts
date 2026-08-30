/**
 * Extracts the message the API meant a human to read.
 *
 * ExceptionMiddleware answers a rejected request with `{ error, statusCode }`, and the generated
 * NSwag clients wrap that in an ApiException whose own `message` is the generic
 * "An unexpected server error occurred." — the useful text sits unparsed in `response`. Reading
 * `err.error` (the plain HttpErrorResponse shape) therefore misses it and silently falls back.
 *
 * Both shapes are handled so this works whether the call went through a generated client or
 * HttpClient directly.
 */
export function apiErrorMessage(err: unknown, fallback: string): string {
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

  return fallback;
}

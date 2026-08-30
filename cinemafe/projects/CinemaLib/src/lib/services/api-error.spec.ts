import { apiErrorMessage } from './api-error';

// The shape that matters most is the NSwag one: ApiException.message is always the generic
// "An unexpected server error occurred.", so reading it (or err.error) loses the real reason a
// request was rejected — which is how a 400 ends up looking like a silent no-op in the UI.
describe('apiErrorMessage', () => {
  const fallback = 'Could not save.';

  it('reads the message out of an NSwag ApiException response body', () => {
    const err = {
      message: 'An unexpected server error occurred.',
      status: 400,
      response: JSON.stringify({
        error: "Room class '2D' cannot screen 3D. Pick a 3D-capable room or set the showtime to 2D.",
        statusCode: 400,
      }),
    };

    expect(apiErrorMessage(err, fallback))
      .toBe("Room class '2D' cannot screen 3D. Pick a 3D-capable room or set the showtime to 2D.");
  });

  it('returns a non-JSON ApiException body verbatim', () => {
    expect(apiErrorMessage({ response: 'Plain text failure' }, fallback)).toBe('Plain text failure');
  });

  it('reads a plain HttpErrorResponse whose body Angular already parsed', () => {
    expect(apiErrorMessage({ error: { error: 'Overlapping showtime.' } }, fallback)).toBe('Overlapping showtime.');
  });

  it('accepts message as the body key too', () => {
    expect(apiErrorMessage({ error: { message: 'Bad request.' } }, fallback)).toBe('Bad request.');
  });

  it('falls back when the payload carries nothing readable', () => {
    expect(apiErrorMessage({ response: '{}' }, fallback)).toBe(fallback);
    expect(apiErrorMessage({ status: 500 }, fallback)).toBe(fallback);
    expect(apiErrorMessage(null, fallback)).toBe(fallback);
    expect(apiErrorMessage(undefined, fallback)).toBe(fallback);
  });
});

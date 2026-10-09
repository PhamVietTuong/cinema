import { API_ERRORS_EN, API_ERRORS_VI } from '../i18n/api-error-translations';
import { API_ERROR_RULES } from './api-error-catalog';
import { apiErrorMessage, registerApiErrorTranslator, translateApiError } from './api-error';

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

describe('api error translation', () => {
  const translatorFor = (dictionary: Record<string, string>) =>
    (key: string, params: Record<string, string>) => {
      const text = dictionary[key];
      if (text === undefined) {
        return null;
      }
      return text.replace(/\{\{(\w+)\}\}/g, (_, name: string) => params[name] ?? '');
    };

  afterEach(() => registerApiErrorTranslator(null));

  it('translates an exact message', () => {
    registerApiErrorTranslator(translatorFor(API_ERRORS_VI));

    expect(translateApiError('This theater has no active checklist of that kind.'))
      .toBe('Rạp này chưa có danh sách kiểm tra nào đang dùng thuộc loại đó.');
  });

  it('translates an interpolated message with its captured params', () => {
    registerApiErrorTranslator(translatorFor(API_ERRORS_VI));

    expect(translateApiError('3 required item(s) are not done yet.'))
      .toBe('Còn 3 mục bắt buộc chưa hoàn thành.');
    expect(translateApiError("'Dune' runs 155 minutes; the showtime must end no earlier than 21:30."))
      .toBe("'Dune' dài 155 phút; suất chiếu phải kết thúc không sớm hơn 21:30.");
    expect(translateApiError('Movie 3f2a9c1e-0000-4000-8000-000000000001 not found.'))
      .toBe('Không tìm thấy phim 3f2a9c1e-0000-4000-8000-000000000001.');
  });

  it('passes an unknown message through unchanged', () => {
    registerApiErrorTranslator(translatorFor(API_ERRORS_VI));

    expect(translateApiError('Something the catalog has never heard of.'))
      .toBe('Something the catalog has never heard of.');
  });

  it('follows the language the translator resolves against', () => {
    const message = 'You are not clocked in.';

    registerApiErrorTranslator(translatorFor(API_ERRORS_VI));
    expect(translateApiError(message)).toBe('Bạn chưa chấm công vào.');

    registerApiErrorTranslator(translatorFor(API_ERRORS_EN));
    expect(translateApiError(message)).toBe(message);
  });

  it('returns the raw text while no translator is registered', () => {
    expect(translateApiError('You are not clocked in.')).toBe('You are not clocked in.');
  });

  it('translates the message pulled out of an NSwag ApiException, but never the caller fallback', () => {
    registerApiErrorTranslator(translatorFor(API_ERRORS_VI));
    const err = { response: JSON.stringify({ error: 'Invalid credentials.', statusCode: 401 }) };

    expect(apiErrorMessage(err, 'Could not save.')).toBe('Thông tin đăng nhập không đúng.');
    expect(apiErrorMessage({ status: 500 }, 'Could not save.')).toBe('Could not save.');
  });

  it('has a Vietnamese and an English text for every rule', () => {
    const missing = API_ERROR_RULES
      .filter(rule => !API_ERRORS_VI[rule.key] || !API_ERRORS_EN[rule.key])
      .map(rule => rule.key);

    expect(missing).toEqual([]);
  });
});

import { TokenStorage } from './token-storage';

// The storage keys are a cross-cutting contract: the HTTP interceptor and the Playwright specs
// both read 'cinema_token' directly. Renaming them silently signs every user out.
const TOKEN_KEY = 'cinema_token';
const USER_KEY = 'cinema_user';

describe('TokenStorage', () => {
  const user = { id: 'u1', name: 'Nguyen Van A' };

  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
  });

  it('persists to localStorage when remember is true', () => {
    TokenStorage.save('tok', user, true);

    expect(localStorage.getItem(TOKEN_KEY)).toBe('tok');
    expect(sessionStorage.getItem(TOKEN_KEY)).toBeNull();
  });

  it('persists to sessionStorage when remember is false', () => {
    TokenStorage.save('tok', user, false);

    expect(sessionStorage.getItem(TOKEN_KEY)).toBe('tok');
    expect(localStorage.getItem(TOKEN_KEY)).toBeNull();
  });

  it('drops the copy in the other store so only one token is authoritative', () => {
    TokenStorage.save('remembered', user, true);
    TokenStorage.save('session-only', user, false);

    expect(localStorage.getItem(TOKEN_KEY)).toBeNull();
    expect(sessionStorage.getItem(TOKEN_KEY)).toBe('session-only');
    expect(TokenStorage.getToken()).toBe('session-only');
  });

  it('reads back a session-scoped token when localStorage is empty', () => {
    TokenStorage.save('tok', user, false);

    expect(TokenStorage.getToken()).toBe('tok');
    expect(JSON.parse(TokenStorage.getUser()!)).toEqual(user);
  });

  it('prefers localStorage over sessionStorage on read', () => {
    localStorage.setItem(TOKEN_KEY, 'from-local');
    sessionStorage.setItem(TOKEN_KEY, 'from-session');

    expect(TokenStorage.getToken()).toBe('from-local');
  });

  it('clear() empties both stores', () => {
    localStorage.setItem(TOKEN_KEY, 'a');
    localStorage.setItem(USER_KEY, '{}');
    sessionStorage.setItem(TOKEN_KEY, 'b');
    sessionStorage.setItem(USER_KEY, '{}');

    TokenStorage.clear();

    expect(TokenStorage.getToken()).toBeNull();
    expect(TokenStorage.getUser()).toBeNull();
  });

  it('returns null when nothing is stored', () => {
    expect(TokenStorage.getToken()).toBeNull();
    expect(TokenStorage.getUser()).toBeNull();
  });
});

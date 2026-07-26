// Auth token persistence. "Remember me" chooses localStorage (survives browser
// close) vs sessionStorage (cleared on close). Reads fall back across both so a
// session persisted either way is picked up on reload.

const TOKEN_KEY = 'cinema_token';
const USER_KEY = 'cinema_user';

export const TokenStorage = {
  save(token: string, user: unknown, remember: boolean): void {
    const primary = remember ? localStorage : sessionStorage;
    const secondary = remember ? sessionStorage : localStorage;
    // Drop any copy in the other store so only one authoritative token exists.
    secondary.removeItem(TOKEN_KEY);
    secondary.removeItem(USER_KEY);
    primary.setItem(TOKEN_KEY, token);
    primary.setItem(USER_KEY, JSON.stringify(user));
  },
  /**
   * Replaces the cached user without touching the token, writing to whichever store already
   * holds the session so the "remember me" choice is preserved. Used after a profile edit:
   * otherwise the stale copy persisted here outlived a full page reload.
   */
  saveUser(user: unknown): void {
    const target = localStorage.getItem(TOKEN_KEY) !== null ? localStorage : sessionStorage;
    target.setItem(USER_KEY, JSON.stringify(user));
  },
  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY) ?? sessionStorage.getItem(TOKEN_KEY);
  },
  getUser(): string | null {
    return localStorage.getItem(USER_KEY) ?? sessionStorage.getItem(USER_KEY);
  },
  clear(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    sessionStorage.removeItem(TOKEN_KEY);
    sessionStorage.removeItem(USER_KEY);
  },
};

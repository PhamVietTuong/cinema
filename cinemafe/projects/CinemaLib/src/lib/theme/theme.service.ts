import { Injectable, computed, signal } from '@angular/core';

export type AppTheme = 'light' | 'dark';

const STORAGE_KEY = 'cinema_theme';
const DEFAULT_THEME: AppTheme = 'light';

/**
 * Central light/dark state for both apps. Persists the choice to localStorage
 * (mirroring the LanguageService pattern), falls back to the OS preference on a
 * first visit, and reflects the active theme as `data-theme` on <html> so the
 * stylesheets can swap their design tokens.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly current = signal<AppTheme>(DEFAULT_THEME);
  readonly isDark = computed(() => this.current() === 'dark');

  /** Called once at startup (via provideAppInitializer) to apply the saved theme. */
  init(): void {
    this.use(this.read());
  }

  use(theme: AppTheme): void {
    const next = theme === 'dark' ? 'dark' : 'light';
    this.current.set(next);
    this.persist(next);
    if (typeof document !== 'undefined') {
      document.documentElement.setAttribute('data-theme', next);
    }
  }

  toggle(): void {
    this.use(this.current() === 'dark' ? 'light' : 'dark');
  }

  private read(): AppTheme {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      if (saved === 'light' || saved === 'dark') {
        return saved;
      }
    } catch {
      /* storage unavailable — fall through to the OS preference */
    }
    return this.prefersDark() ? 'dark' : DEFAULT_THEME;
  }

  private persist(theme: AppTheme): void {
    try {
      localStorage.setItem(STORAGE_KEY, theme);
    } catch {
      /* storage unavailable — keep in-memory state only */
    }
  }

  private prefersDark(): boolean {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
      return false;
    }
    return window.matchMedia('(prefers-color-scheme: dark)').matches;
  }
}

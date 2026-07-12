import { Injectable, inject, signal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

export type AppLanguage = 'vi' | 'en';

export interface LanguageOption {
  code: AppLanguage;
  /** Native name, always shown in its own language. */
  label: string;
}

const STORAGE_KEY = 'cinema_lang';
const DEFAULT_LANG: AppLanguage = 'vi';

export const SUPPORTED_LANGUAGES: readonly LanguageOption[] = [
  { code: 'vi', label: 'Tiếng Việt' },
  { code: 'en', label: 'English' },
];

/**
 * Central language state for both apps. Wraps ngx-translate's TranslateService,
 * persists the chosen locale to localStorage (mirroring the existing theme
 * pattern), and keeps a signal so templates react to switches.
 */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly translate = inject(TranslateService);

  readonly current = signal<AppLanguage>(DEFAULT_LANG);
  readonly languages = SUPPORTED_LANGUAGES;

  /** Called once at startup (via APP_INITIALIZER) to apply the saved locale. */
  init(): void {
    this.translate.addLangs(SUPPORTED_LANGUAGES.map((l) => l.code));
    this.translate.setFallbackLang(DEFAULT_LANG);
    this.use(this.read());
  }

  use(lang: AppLanguage): void {
    const next = this.isSupported(lang) ? lang : DEFAULT_LANG;
    this.translate.use(next);
    this.current.set(next);
    this.persist(next);
    if (typeof document !== 'undefined') {
      document.documentElement.lang = next;
    }
  }

  toggle(): void {
    this.use(this.current() === 'vi' ? 'en' : 'vi');
  }

  private isSupported(lang: string): lang is AppLanguage {
    return SUPPORTED_LANGUAGES.some((l) => l.code === lang);
  }

  private read(): AppLanguage {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      return saved && this.isSupported(saved) ? saved : DEFAULT_LANG;
    } catch {
      return DEFAULT_LANG;
    }
  }

  private persist(lang: AppLanguage): void {
    try {
      localStorage.setItem(STORAGE_KEY, lang);
    } catch {
      /* storage unavailable — keep in-memory state only */
    }
  }
}

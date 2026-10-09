import {
  EnvironmentProviders,
  Provider,
  inject,
  provideAppInitializer,
} from '@angular/core';
import { TranslateLoader, TranslateService, provideTranslateService } from '@ngx-translate/core';
import { registerApiErrorTranslator } from '../services/api-error';
import { LanguageService } from './language.service';
import { CinemaTranslateLoader } from './cinema-base-translations';

/**
 * One-line translation wiring for an app's root providers. Loads JSON
 * dictionaries from `/assets/i18n/{lang}.json` (served from each app's
 * `public/` folder), falls back to Vietnamese, and applies the persisted
 * locale on startup.
 *
 * Requires `provideHttpClient(...)` to also be present in the same providers.
 */
export function provideCinemaTranslation(): (Provider | EnvironmentProviders)[] {
  return [
    provideTranslateService({
      loader: { provide: TranslateLoader, useClass: CinemaTranslateLoader },
      fallbackLang: 'vi',
    }),
    provideAppInitializer(() => inject(LanguageService).init()),
    provideAppInitializer(() => {
      const translate = inject(TranslateService);
      registerApiErrorTranslator((key, params) => {
        const path = `apiErrors.${key}`;
        const text = translate.instant(path, params);
        return text === path ? null : text;
      });
    }),
  ];
}

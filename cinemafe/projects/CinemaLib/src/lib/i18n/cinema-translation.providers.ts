import {
  EnvironmentProviders,
  Provider,
  inject,
  provideAppInitializer,
} from '@angular/core';
import { TranslateLoader, provideTranslateService } from '@ngx-translate/core';
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
  ];
}

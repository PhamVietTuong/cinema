import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { TranslateLoader, TranslationObject } from '@ngx-translate/core';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

/**
 * Keys every app shares (shell chrome and the sign-in form). They are merged UNDER each app's own
 * `/assets/i18n/{lang}.json`, so an app can still override any of them by redefining the key.
 */
export const CINEMA_BASE_TRANSLATIONS: Record<string, TranslationObject> = {
  vi: {
    nav: { logout: 'Đăng Xuất' },
    topbar: {
      lightMode: 'Chế độ sáng',
      darkMode: 'Chế độ tối',
      notifications: 'Thông báo',
    },
    shell: {
      userFallback: 'Người dùng',
      defaultTitle: 'Bảng Điều Khiển',
    },
    login: {
      emailOrPhoneLabel: 'Email hoặc Số Điện Thoại',
      emailOrPhonePlaceholder: 'Nhập email hoặc số điện thoại',
      passwordLabel: 'Mật Khẩu',
      showPassword: 'Hiện mật khẩu',
      hidePassword: 'Ẩn mật khẩu',
      rememberMe: 'Ghi nhớ đăng nhập',
      forgotPassword: 'Quên mật khẩu?',
      submit: 'Đăng Nhập',
    },
  },
  en: {
    nav: { logout: 'Log Out' },
    topbar: {
      lightMode: 'Light mode',
      darkMode: 'Dark mode',
      notifications: 'Notifications',
    },
    shell: {
      userFallback: 'User',
      defaultTitle: 'Control Panel',
    },
    login: {
      emailOrPhoneLabel: 'Email or Phone Number',
      emailOrPhonePlaceholder: 'Enter email or phone number',
      passwordLabel: 'Password',
      showPassword: 'Show password',
      hidePassword: 'Hide password',
      rememberMe: 'Remember me',
      forgotPassword: 'Forgot password?',
      submit: 'Sign In',
    },
  },
};

function isObject(value: unknown): value is TranslationObject {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function deepMerge(base: TranslationObject, override: TranslationObject): TranslationObject {
  const result: TranslationObject = { ...base };
  for (const key of Object.keys(override)) {
    const baseValue = base[key];
    const overrideValue = override[key];
    result[key] = isObject(baseValue) && isObject(overrideValue) ? deepMerge(baseValue, overrideValue) : overrideValue;
  }
  return result;
}

/** Loads the app's `/assets/i18n/{lang}.json` and layers it over {@link CINEMA_BASE_TRANSLATIONS}. */
@Injectable()
export class CinemaTranslateLoader extends TranslateLoader {
  private readonly _http = inject(HttpClient);

  getTranslation(lang: string): Observable<TranslationObject> {
    const base = CINEMA_BASE_TRANSLATIONS[lang] ?? {};
    return this._http.get<TranslationObject>(`/assets/i18n/${lang}.json`).pipe(
      map(app => deepMerge(base, app)),
      catchError(() => of(base)),
    );
  }
}

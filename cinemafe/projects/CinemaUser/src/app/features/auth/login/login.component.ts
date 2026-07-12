import { AfterViewInit, ChangeDetectorRef, Component, ElementRef, ViewChild, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { AbstractControl, FormBuilder, FormGroup, ValidationErrors, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { Store } from '@ngrx/store';
import {
  SharedModule, login, loginSuccess,
  selectAuthLoading, selectAuthError, selectAwaitingTwoFactor,
} from 'CinemaLib';
import { TranslateService } from '@ngx-translate/core';
import { environment } from '../../../../environments/environment';

// Provided by the Google Identity Services script loaded in index.html.
declare const google: any;

// Accepts either an email or a phone number (the backend logs in with either).
function emailOrPhoneValidator(control: AbstractControl): ValidationErrors | null {
  const value = (control.value ?? '').trim();
  if (!value) return null; // `required` reports the empty case.
  const emailRe = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  const phoneRe = /^\+?\d[\d\s-]{7,}$/;
  return emailRe.test(value) || phoneRe.test(value) ? null : { emailOrPhone: true };
}

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent implements AfterViewInit {
  private _store = inject(Store);
  private _fb = inject(FormBuilder);
  private _http = inject(HttpClient);
  private _cdr = inject(ChangeDetectorRef);
  private _translate = inject(TranslateService);

  @ViewChild('googleBtn') googleBtn?: ElementRef<HTMLElement>;

  hidePass = true;
  capsLockOn = false;
  verifying = false;
  otpError: string | null = null;
  googleError: string | null = null;
  googleEnabled = !!environment.googleClientId;
  facebookError: string | null = null;
  facebookEnabled = !!environment.facebookAppId;
  private _fbReady?: Promise<void>;

  loading$: Observable<boolean> = this._store.select(selectAuthLoading);
  error$: Observable<string | null> = this._store.select(selectAuthError);
  awaitingTwoFactor$: Observable<boolean> = this._store.select(selectAwaitingTwoFactor);

  form: FormGroup = this._fb.group({
    emailOrPhone: ['', [Validators.required, emailOrPhoneValidator]],
    password: ['', [Validators.required]],
    rememberMe: [false],
  });

  otpForm: FormGroup = this._fb.group({
    code: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]],
  });

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { emailOrPhone, password, rememberMe } = this.form.value;
    this._store.dispatch(login({ request: { emailOrPhone, password } as any, rememberMe }));
  }

  // Second step: exchange the emailed 6-digit code for a token.
  verifyOtp(): void {
    if (this.otpForm.invalid) {
      this.otpForm.markAllAsTouched();
      return;
    }
    this.verifying = true;
    this.otpError = null;
    this._http.post(`${environment.apiUrl}/api/Identity/VerifyTwoFactor`, {
      emailOrPhone: this.form.value.emailOrPhone,
      code: this.otpForm.value.code,
    }).subscribe({
      next: (response: any) =>
        this._store.dispatch(loginSuccess({ response, rememberMe: this.form.value.rememberMe })),
      error: () => {
        this.verifying = false;
        this.otpError = this._translate.instant('auth.login.otpError');
        this._cdr.markForCheck();
      },
    });
  }

  onPasswordKey(event: KeyboardEvent): void {
    this.capsLockOn = event.getModifierState?.('CapsLock') ?? false;
  }

  ngAfterViewInit(): void {
    if (this.googleEnabled) this._initGoogle();
  }

  // The GIS script may not be ready yet; retry briefly until `google.accounts.id` exists.
  private _initGoogle(retries = 20): void {
    if (typeof google === 'undefined' || !google.accounts?.id) {
      if (retries > 0) setTimeout(() => this._initGoogle(retries - 1), 150);
      return;
    }
    google.accounts.id.initialize({
      client_id: environment.googleClientId,
      callback: (resp: { credential: string }) => this._onGoogleCredential(resp),
    });
    if (this.googleBtn) {
      google.accounts.id.renderButton(this.googleBtn.nativeElement, { theme: 'outline', size: 'large', width: 360 });
    }
  }

  // Lazily loads the Facebook JS SDK and initialises it with the configured app id.
  private _ensureFbSdk(): Promise<void> {
    if (this._fbReady) { return this._fbReady; }
    this._fbReady = new Promise<void>((resolve) => {
      const w = window as any;
      if (w.FB) { resolve(); return; }
      w.fbAsyncInit = () => {
        w.FB.init({ appId: environment.facebookAppId, cookie: true, xfbml: false, version: 'v19.0' });
        resolve();
      };
      const s = document.createElement('script');
      s.src = 'https://connect.facebook.net/en_US/sdk.js';
      s.async = true;
      s.defer = true;
      document.body.appendChild(s);
    });
    return this._fbReady;
  }

  loginWithFacebook(): void {
    this.facebookError = null;
    this._ensureFbSdk().then(() => {
      (window as any).FB.login((resp: any) => {
        const token = resp?.authResponse?.accessToken;
        if (!token) { return; } // user cancelled
        this._http.post(`${environment.apiUrl}/api/Identity/FacebookLogin`, { accessToken: token }).subscribe({
          next: (response: any) => this._store.dispatch(loginSuccess({ response, rememberMe: this.form.value.rememberMe })),
          error: () => {
            this.facebookError = this._translate.instant('auth.login.facebookError');
            this._cdr.markForCheck();
          },
        });
      }, { scope: 'public_profile,email' });
    });
  }

  private _onGoogleCredential(resp: { credential: string }): void {
    this.googleError = null;
    this._http.post(`${environment.apiUrl}/api/Identity/GoogleLogin`, { idToken: resp.credential }).subscribe({
      next: (response: any) => this._store.dispatch(loginSuccess({ response, rememberMe: this.form.value.rememberMe })),
      error: () => {
        this.googleError = this._translate.instant('auth.login.googleError');
        this._cdr.markForCheck();
      },
    });
  }
}

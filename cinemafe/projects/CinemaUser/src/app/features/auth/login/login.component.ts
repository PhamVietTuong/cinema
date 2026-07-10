import { ChangeDetectorRef, Component, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { AbstractControl, FormBuilder, FormGroup, ValidationErrors, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { Store } from '@ngrx/store';
import {
  SharedModule, login, loginSuccess,
  selectAuthLoading, selectAuthError, selectAwaitingTwoFactor,
} from 'CinemaLib';
import { environment } from '../../../../environments/environment';

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
export class LoginComponent {
  private _store = inject(Store);
  private _fb = inject(FormBuilder);
  private _http = inject(HttpClient);
  private _cdr = inject(ChangeDetectorRef);

  hidePass = true;
  capsLockOn = false;
  verifying = false;
  otpError: string | null = null;

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
        this.otpError = 'Mã không hợp lệ hoặc đã hết hạn.';
        this._cdr.markForCheck();
      },
    });
  }

  onPasswordKey(event: KeyboardEvent): void {
    this.capsLockOn = event.getModifierState?.('CapsLock') ?? false;
  }
}

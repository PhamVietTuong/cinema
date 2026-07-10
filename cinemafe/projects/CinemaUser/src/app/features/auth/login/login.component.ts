import { Component } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, ValidationErrors, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { Store } from '@ngrx/store';
import { SharedModule, login, selectAuthLoading, selectAuthError } from 'CinemaLib';

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
  hidePass = true;
  capsLockOn = false;
  loading$: Observable<boolean>;
  error$: Observable<string | null>;
  form: FormGroup;

  constructor(
    private _store: Store,
    private _fb: FormBuilder,
  ) {
    this.loading$ = this._store.select(selectAuthLoading);
    this.error$ = this._store.select(selectAuthError);
    this.form = this._fb.group({
      emailOrPhone: ['', [Validators.required, emailOrPhoneValidator]],
      password: ['', [Validators.required]],
      rememberMe: [false],
    });
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { emailOrPhone, password, rememberMe } = this.form.value;
    this._store.dispatch(login({ request: { emailOrPhone, password } as any, rememberMe }));
  }

  onPasswordKey(event: KeyboardEvent): void {
    this.capsLockOn = event.getModifierState?.('CapsLock') ?? false;
  }
}

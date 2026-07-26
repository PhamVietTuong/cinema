import { Component } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, ValidationErrors, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { Store } from '@ngrx/store';
import { SharedModule, register, selectAuthLoading, selectAuthError } from 'CinemaLib';

/** Group validator: password and confirmPassword must match. */
function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const p = group.get('password')?.value;
  const c = group.get('confirmPassword')?.value;
  return p && c && p !== c ? { passwordMismatch: true } : null;
}

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss',
})
export class RegisterComponent {
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
      name: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      phone: ['', [Validators.required, Validators.pattern(/^(?:\+84|0)\d{9,10}$/)]],
      password: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', Validators.required],
    }, { validators: passwordsMatch });
  }

  onSubmit(): void {
    if (this.form.valid) {
      // Send the whole form, confirmPassword included: RegisterRequest declares it
      // [Required] + [Compare(Password)], so stripping it here fails validation with a 400.
      this._store.dispatch(register({ request: this.form.value as any }));
    } else {
      this.form.markAllAsTouched();
    }
  }
}

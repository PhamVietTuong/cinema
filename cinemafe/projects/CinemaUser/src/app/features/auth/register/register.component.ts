import { Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { Store } from '@ngrx/store';
import { SharedModule, register, selectAuthLoading, selectAuthError } from 'CinemaLib';

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
      phone: ['', Validators.required],
      password: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: [''],
    });
  }

  onSubmit(): void {
    if (this.form.valid) {
      this._store.dispatch(register({ request: this.form.value as any }));
    }
  }
}

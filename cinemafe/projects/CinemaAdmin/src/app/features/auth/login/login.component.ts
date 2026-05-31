import { Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { Store } from '@ngrx/store';
import { SharedModule, login, selectAuthLoading, selectAuthError } from 'CinemaLib';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  hidePass = true;
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
      emailOrPhone: ['', Validators.required],
      password: ['', Validators.required],
    });
  }

  onSubmit(): void {
    if (this.form.valid) {
      this._store.dispatch(login({ request: this.form.value as any }));
    }
  }
}

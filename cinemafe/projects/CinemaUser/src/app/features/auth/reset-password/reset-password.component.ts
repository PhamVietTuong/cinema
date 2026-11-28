import { ChangeDetectorRef, Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { SharedModule, passwordsMatch } from 'CinemaLib';
import { TranslateService } from '@ngx-translate/core';
import { environment } from '../../../../environments/environment';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-reset-password',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './reset-password.component.html',
  styleUrl: '../login/login.component.scss',
})
export class ResetPasswordComponent {
  private _http = inject(HttpClient);
  private _fb = inject(FormBuilder);
  private _route = inject(ActivatedRoute);
  private _router = inject(Router);
  private _cdr = inject(ChangeDetectorRef);
  private _translate = inject(TranslateService);

  private _email = this._route.snapshot.queryParamMap.get('email') ?? '';
  private _token = this._route.snapshot.queryParamMap.get('token') ?? '';

  form: FormGroup = this._fb.group({
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', Validators.required],
  }, { validators: passwordsMatch('newPassword') });
  loading = false;
  error: string | null = null;
  invalidLink = !this._email || !this._token;

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.loading = true;
    this.error = null;
    this._http.post(`${environment.apiUrl}/api/Identity/ResetPassword`, {
      email: this._email,
      token: this._token,
      newPassword: this.form.value.newPassword,
    }).subscribe({
      next: () => this._router.navigate(['/auth/login']),
      error: () => {
        this.loading = false;
        this.error = this._translate.instant('auth.reset.error');
        this._cdr.markForCheck();
      },
    });
  }
}

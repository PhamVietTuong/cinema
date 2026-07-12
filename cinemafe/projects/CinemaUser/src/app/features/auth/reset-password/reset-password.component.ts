import { ChangeDetectorRef, Component, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { AbstractControl, FormBuilder, FormGroup, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { SharedModule } from 'CinemaLib';
import { TranslateService } from '@ngx-translate/core';
import { environment } from '../../../../environments/environment';

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const p = group.get('newPassword')?.value;
  const c = group.get('confirmPassword')?.value;
  return p && c && p !== c ? { passwordMismatch: true } : null;
}

@Component({
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
  }, { validators: passwordsMatch });
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

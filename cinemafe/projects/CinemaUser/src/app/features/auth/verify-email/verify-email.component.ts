import { ChangeDetectorRef, Component, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { SharedModule } from 'CinemaLib';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-verify-email',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './verify-email.component.html',
  styleUrl: '../login/login.component.scss',
})
export class VerifyEmailComponent {
  private _http = inject(HttpClient);
  private _fb = inject(FormBuilder);
  private _route = inject(ActivatedRoute);
  private _cdr = inject(ChangeDetectorRef);

  private _email = this._route.snapshot.queryParamMap.get('email') ?? '';
  private _token = this._route.snapshot.queryParamMap.get('token') ?? '';

  // A link (email + token) confirms directly; otherwise we show the resend form.
  hasLink = !!this._email && !!this._token;
  loading = false;
  status: 'pending' | 'confirmed' | 'error' | 'resent' = 'pending';

  resendForm: FormGroup = this._fb.group({
    email: [this._email, [Validators.required, Validators.email]],
  });

  constructor() {
    if (this.hasLink) this._confirm();
  }

  private _confirm(): void {
    this.loading = true;
    this._http.post(`${environment.apiUrl}/api/Identity/ConfirmEmail`, {
      email: this._email,
      token: this._token,
    }).subscribe({
      next: () => { this.loading = false; this.status = 'confirmed'; this._cdr.markForCheck(); },
      error: () => { this.loading = false; this.status = 'error'; this._cdr.markForCheck(); },
    });
  }

  resend(): void {
    if (this.resendForm.invalid) { this.resendForm.markAllAsTouched(); return; }
    this.loading = true;
    this._http.post(`${environment.apiUrl}/api/Identity/ResendVerification`, {
      email: this.resendForm.value.email,
    }).subscribe({
      // The backend is intentionally silent about whether the address exists.
      next: () => { this.loading = false; this.status = 'resent'; this._cdr.markForCheck(); },
      error: () => { this.loading = false; this.status = 'resent'; this._cdr.markForCheck(); },
    });
  }
}

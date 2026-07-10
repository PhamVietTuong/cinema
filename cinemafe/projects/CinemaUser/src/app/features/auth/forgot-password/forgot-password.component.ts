import { ChangeDetectorRef, Component, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { SharedModule } from 'CinemaLib';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './forgot-password.component.html',
  styleUrl: '../login/login.component.scss',
})
export class ForgotPasswordComponent {
  private _http = inject(HttpClient);
  private _fb = inject(FormBuilder);
  private _cdr = inject(ChangeDetectorRef);

  form: FormGroup = this._fb.group({
    email: ['', [Validators.required, Validators.email]],
  });
  loading = false;
  sent = false;

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.loading = true;
    // Always show the same confirmation regardless of outcome — never reveal whether the email exists.
    this._http.post(`${environment.apiUrl}/api/Identity/ForgotPassword`, this.form.value).subscribe({
      next: () => this._done(),
      error: () => this._done(),
    });
  }

  private _done(): void {
    this.loading = false;
    this.sent = true;
    this._cdr.markForCheck();
  }
}

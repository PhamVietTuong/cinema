import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { TranslateService } from '@ngx-translate/core';
import { SharedModule, IdentityServiceAgent } from 'CinemaLib';

@Component({
  selector: 'app-admin-profile',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
})
export class ProfileComponent implements OnInit {
  private _identity = inject(IdentityServiceAgent.HttpService);
  private _fb = inject(FormBuilder);
  private _cdr = inject(ChangeDetectorRef);
  private _translate = inject(TranslateService);

  tab: 'info' | 'password' = 'info';
  user: IdentityServiceAgent.UserDTO | null = null;

  profileForm: FormGroup = this._fb.group({
    name: ['', Validators.required],
    phone: [''],
    avatar: [''],
  });
  passwordForm: FormGroup = this._fb.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(6)]],
    confirmNewPassword: ['', Validators.required],
  });

  profileMsg = ''; profileErr = '';
  passwordMsg = ''; passwordErr = '';

  ngOnInit(): void {
    this._identity.getProfile().subscribe({
      next: u => {
        this.user = u;
        this.profileForm.patchValue({ name: u.name ?? '', phone: u.phone ?? '', avatar: u.avatar ?? '' });
        this._cdr.markForCheck();
      },
      error: () => this._cdr.markForCheck(),
    });
  }

  saveProfile(): void {
    if (this.profileForm.invalid) { this.profileForm.markAllAsTouched(); return; }
    this.profileMsg = ''; this.profileErr = '';
    this._identity.updateProfile(IdentityServiceAgent.UpdateProfileRequest.fromJS(this.profileForm.value))
      .subscribe({
        next: () => {
          this.profileMsg = this._translate.instant('profile.profileUpdateSuccess');
          this._identity.getProfile().subscribe(u => { this.user = u; this._cdr.markForCheck(); });
          this._cdr.markForCheck();
        },
        error: e => { this.profileErr = this._err(e, this._translate.instant('profile.profileUpdateFailed')); this._cdr.markForCheck(); },
      });
  }

  changePassword(): void {
    if (this.passwordForm.invalid) { this.passwordForm.markAllAsTouched(); return; }
    const v = this.passwordForm.value;
    this.passwordMsg = ''; this.passwordErr = '';
    if (v.newPassword !== v.confirmNewPassword) { this.passwordErr = this._translate.instant('profile.passwordMismatch'); return; }
    this._identity.changePassword(IdentityServiceAgent.ChangePasswordRequest.fromJS(v))
      .subscribe({
        next: () => { this.passwordMsg = this._translate.instant('profile.passwordChangeSuccess'); this.passwordForm.reset(); this._cdr.markForCheck(); },
        error: e => { this.passwordErr = this._err(e, this._translate.instant('profile.passwordChangeFailed')); this._cdr.markForCheck(); },
      });
  }

  initials(name?: string): string {
    const parts = (name ?? '').trim().split(/\s+/);
    return ((parts[0]?.[0] ?? '') + (parts[parts.length - 1]?.[0] ?? '')).toUpperCase() || 'QT';
  }

  private _err(e: any, fallback: string): string {
    const x = e?.error;
    return (typeof x === 'string' && x) ? x : (x?.error || x?.message || fallback);
  }
}

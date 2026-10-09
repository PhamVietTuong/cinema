import { ChangeDetectorRef, Directive, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, ValidatorFn, Validators } from '@angular/forms';
import { TranslateService } from '@ngx-translate/core';
import { Store } from '@ngrx/store';
import { IdentityServiceAgent } from '../../services/identity-http.service';
import { apiErrorMessage } from '../../services/api-error';
import { profileUpdated } from '../../store/auth/auth.actions';

/**
 * Shared "account" + "change password" form logic behind a profile page. A subclass supplies the
 * per-app i18n keys, initials fallback and phone validator, and may extend `ngOnInit`/react to the
 * loaded user via `_onProfileLoaded`.
 *
 * Ordering gotcha (same as `BaseTableComponent`): `_phoneValidators()` is called from the
 * `profileForm` field initializer, which runs from inside `super()` — i.e. BEFORE the subclass's
 * own field initializers and constructor body run. Override it as a METHOD, never read a subclass
 * field from it.
 */
@Directive()
export abstract class ProfileFormBase implements OnInit {
  protected readonly _identity = inject(IdentityServiceAgent.HttpService);
  protected readonly _fb = inject(FormBuilder);
  protected readonly _cdr = inject(ChangeDetectorRef);
  protected readonly _translate = inject(TranslateService);
  protected readonly _store = inject(Store);

  user: IdentityServiceAgent.UserDTO | null = null;

  profileForm: FormGroup = this._fb.group({
    name: ['', Validators.required],
    phone: ['', this._phoneValidators()],
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
        this._onProfileLoaded(u);
        this._cdr.markForCheck();
      },
      error: () => this._cdr.markForCheck(),
    });
  }

  saveProfile(): void {
    if (this.profileForm.invalid) { this.profileForm.markAllAsTouched(); return; }
    this.profileMsg = ''; this.profileErr = '';
    const i18n = this._profileI18n();
    this._identity.updateProfile(IdentityServiceAgent.UpdateProfileRequest.fromJS(this.profileForm.value))
      .subscribe({
        next: () => {
          this.profileMsg = this._translate.instant(i18n.updateSuccess);
          this._identity.getProfile().subscribe(u => {
            this.user = u;
            // Keep the cached auth user in step, otherwise the header/sidebar keeps showing the old
            // name — and keeps showing it after a reload, since storage still holds the old copy.
            this._store.dispatch(profileUpdated({ user: u }));
            this._cdr.markForCheck();
          });
          this._cdr.markForCheck();
        },
        error: e => { this.profileErr = this._err(e, this._translate.instant(i18n.updateFailed)); this._cdr.markForCheck(); },
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
    return ((parts[0]?.[0] ?? '') + (parts[parts.length - 1]?.[0] ?? '')).toUpperCase() || this._initialsFallback();
  }

  protected _err(e: unknown, fallback: string): string {
    return apiErrorMessage(e, fallback);
  }

  /** Extra phone-field validators; default none. Override as a method — see the class doc comment. */
  protected _phoneValidators(): ValidatorFn[] {
    return [];
  }

  /** i18n keys for the profile-save success/failure messages. */
  protected abstract _profileI18n(): { updateSuccess: string; updateFailed: string };

  /** Fallback initials shown when the user has no name. */
  protected abstract _initialsFallback(): string;

  /** Extension point for subclass state that depends on the freshly loaded user (e.g. notification prefs). */
  protected _onProfileLoaded(_user: IdentityServiceAgent.UserDTO): void {
    // no-op by default
  }
}

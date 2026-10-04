import { ChangeDetectionStrategy, Component, DestroyRef, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, Validators } from '@angular/forms';
import { Actions, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { SharedModule } from '../../shared.module';
import { ShellBrand } from '../shell/app-shell.component';
import { login, loginSuccess } from '../../store/auth/auth.actions';
import { selectAuthError, selectAuthLoading } from '../../store/auth/auth.selectors';

/** One chip in the brand panel. */
export interface LoginStat {
  icon: string;
  labelKey: string;
}

/**
 * Split-screen sign-in page shared by CinemaAdmin and CinemaStaff: a dark brand panel on the left
 * and the credentials card on the right. It dispatches the NgRx `login` action; the AuthEffects
 * handle the redirect. Each app keeps a thin page that supplies the copy (i18n keys) and, if it
 * restricts who may sign in, `allowedRoles` and a `rejected` handler.
 */
@Component({
  selector: 'cl-login-form',
  standalone: true,
  imports: [SharedModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login-form.component.html',
  styleUrl: './login-form.component.scss',
})
export class LoginFormComponent {
  readonly brand = input.required<ShellBrand>();
  /** i18n key of the card heading. */
  readonly titleKey = input.required<string>();
  readonly subtitleKey = input<string | null>(null);
  readonly heroTitleLine1Key = input<string | null>(null);
  readonly heroTitleLine2Key = input<string | null>(null);
  readonly heroSubtitleKey = input<string | null>(null);
  readonly stats = input<readonly LoginStat[]>([]);
  readonly footerKey = input<string | null>(null);
  /** Material icon in the badge above the card heading. */
  readonly icon = input<string>('admin_panel_settings');
  /** i18n key of an extra notice shown above the submit button (e.g. "this account may not use this app"). */
  readonly noticeKey = input<string | null>(null);
  /** When set, a successful sign-in with any other role emits `rejected` with that role. */
  readonly allowedRoles = input<readonly string[] | null>(null);
  readonly rejected = output<string>();

  readonly hidePass = signal(true);

  private readonly _store = inject(Store);
  private readonly _fb = inject(FormBuilder);

  readonly loading = this._store.selectSignal(selectAuthLoading);
  readonly error = this._store.selectSignal(selectAuthError);

  readonly form = this._fb.group({
    emailOrPhone: ['', Validators.required],
    password: ['', Validators.required],
  });

  constructor() {
    inject(Actions).pipe(ofType(loginSuccess), takeUntilDestroyed(inject(DestroyRef))).subscribe(({ response }) => {
      const allowed = this.allowedRoles();
      const role = response.user?.userTypeName ?? '';
      if (allowed && !allowed.includes(role)) {
        this.rejected.emit(role);
      }
    });
  }

  onSubmit(): void {
    if (this.form.valid) {
      this._store.dispatch(login({ request: this.form.value as any }));
    }
  }
}

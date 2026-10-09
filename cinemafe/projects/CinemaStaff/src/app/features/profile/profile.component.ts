import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import {
  APPROVER_ROLES,
  SharedModule,
  CinemaServiceAgent,
  hideLoading,
  selectCurrentUser,
  showException,
  showLoading,
  showSuccess,
} from 'CinemaLib';
import { OVERRIDE_PIN_PATTERN, pinsMatchValidator } from './override-pin';

/** Staff profile: account summary plus, for approver roles, the manager override PIN. */
@Component({
  selector: 'staff-profile',
  standalone: true,
  imports: [SharedModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
<div class="ad-page">
  <div class="ad-page-header">
    <div>
      <h1 class="ad-h1">{{ 'profile.title' | translate }}</h1>
      <p class="ad-sub">{{ 'profile.subtitle' | translate }}</p>
    </div>
  </div>

  <mat-card class="ad-card section">
    <h3 class="ad-card-title">{{ 'profile.account' | translate }}</h3>
    <p><strong>{{ user()?.name }}</strong></p>
    <p class="muted">{{ user()?.email }}</p>
    <p class="muted">{{ user()?.userTypeName }}</p>
  </mat-card>

  @if (isApprover()) {
    <mat-card class="ad-card section">
      <h3 class="ad-card-title">{{ 'pinSettings.title' | translate }}</h3>
      <p class="muted">{{ 'pinSettings.hint' | translate }}</p>
      <form [formGroup]="form" (ngSubmit)="save()" class="pin-form">
        <mat-form-field appearance="outline">
          <mat-label>{{ 'pinSettings.pin' | translate }}</mat-label>
          <input matInput type="password" inputmode="numeric" maxlength="8" autocomplete="new-password" formControlName="pin">
          @if (form.controls.pin.invalid && form.controls.pin.touched) {
            <mat-error>{{ 'pinSettings.pinInvalid' | translate }}</mat-error>
          }
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>{{ 'pinSettings.confirmPin' | translate }}</mat-label>
          <input matInput type="password" inputmode="numeric" maxlength="8" autocomplete="new-password" formControlName="confirmPin">
          @if (form.hasError('pinMismatch') && form.controls.confirmPin.touched) {
            <mat-error>{{ 'pinSettings.mismatch' | translate }}</mat-error>
          }
        </mat-form-field>
        <div>
          <button mat-raised-button color="primary" type="submit" [disabled]="form.invalid || saving()">
            <mat-icon>lock</mat-icon> {{ 'pinSettings.save' | translate }}
          </button>
        </div>
      </form>
    </mat-card>
  }
</div>
`,
  styles: [`
    .section { max-width: 520px; margin-bottom: 16px; padding: 16px 24px; }
    .muted { color: var(--ml-muted); }
    .pin-form { display: flex; flex-direction: column; gap: 4px; }
  `],
})
export class ProfileComponent {
  private readonly _store = inject(Store);
  private readonly _workforce = inject(CinemaServiceAgent.HttpService);
  private readonly _translate = inject(TranslateService);
  private readonly _fb = inject(FormBuilder);

  readonly user = this._store.selectSignal(selectCurrentUser);
  readonly saving = signal(false);

  readonly form = this._fb.nonNullable.group({
    pin: ['', [Validators.required, Validators.pattern(OVERRIDE_PIN_PATTERN)]],
    confirmPin: ['', [Validators.required]],
  }, { validators: pinsMatchValidator });

  isApprover(): boolean {
    return APPROVER_ROLES.includes(this.user()?.userTypeName ?? '');
  }

  save(): void {
    if (this.form.invalid || this.saving()) {
      return;
    }
    this.saving.set(true);
    this._store.dispatch(showLoading());
    this._workforce.setMyOverridePin(CinemaServiceAgent.SetOverridePinRequest.fromJS({ pin: this.form.controls.pin.value })).subscribe({
      next: () => {
        // Never keep the PIN around once saved.
        this.form.reset({ pin: '', confirmPin: '' });
        this._store.dispatch(showSuccess({ message: this._translate.instant('pinSettings.saved') }));
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this.saving.set(false);
      this._store.dispatch(hideLoading());
    });
  }
}

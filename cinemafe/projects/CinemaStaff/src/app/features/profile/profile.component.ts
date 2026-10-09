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
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
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

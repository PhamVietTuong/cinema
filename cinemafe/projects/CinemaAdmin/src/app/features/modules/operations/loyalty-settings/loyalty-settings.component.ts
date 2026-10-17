import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { TranslateService } from '@ngx-translate/core';
import { CinemaServiceAgent, ToastService } from 'CinemaLib';

/** Global default loyalty point conversion rate — applies to any theater without its own override
 * (set per-theater on that theater's "Loyalty Points" tab). */
@Component({
  selector: 'app-loyalty-settings',
  standalone: false,
  templateUrl: './loyalty-settings.component.html',
  styleUrls: ['./loyalty-settings.component.scss'],
})
export class LoyaltySettingsComponent implements OnInit {
  form: FormGroup;
  lastUpdatedTime: Date | null = null;
  loading = false;
  saving = false;

  constructor(
    private _fb: FormBuilder,
    private _cinemaService: CinemaServiceAgent.HttpService,
    private _toast: ToastService,
    private _translate: TranslateService,
    private _cdr: ChangeDetectorRef,
  ) {
    this.form = this._fb.group({
      pointsPerUnit: [1000, [Validators.required, Validators.min(1)]],
      pointValueVnd: [1000, [Validators.required, Validators.min(1)]],
    });
  }

  ngOnInit(): void {
    this.loading = true;
    this._cinemaService.getGlobalLoyaltySetting().subscribe({
      next: setting => {
        this.form.patchValue({ pointsPerUnit: setting.pointsPerUnit, pointValueVnd: setting.pointValueVnd });
        this.lastUpdatedTime = setting.lastUpdatedTime ?? null;
        this.loading = false;
        this._cdr.markForCheck();
      },
      error: () => {
        this.loading = false;
        this._cdr.markForCheck();
      },
    });
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving = true;
    this._cinemaService.saveLoyaltySetting(CinemaServiceAgent.SaveLoyaltySettingRequest.fromJS({
      theaterId: undefined,
      pointsPerUnit: this.form.value.pointsPerUnit,
      pointValueVnd: this.form.value.pointValueVnd,
    })).subscribe({
      next: setting => {
        this.lastUpdatedTime = setting.lastUpdatedTime ?? null;
        this.saving = false;
        this._toast.success(this._translate.instant('loyaltySettings.saved'));
        this._cdr.markForCheck();
      },
      error: e => {
        this.saving = false;
        this._toast.error(e?.error?.error || e?.error?.message || this._translate.instant('loyaltySettings.saveFailed'));
        this._cdr.markForCheck();
      },
    });
  }
}

import { ChangeDetectorRef, Component, Input, OnChanges } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { TranslateService } from '@ngx-translate/core';
import { CinemaServiceAgent, ToastService } from 'CinemaLib';

/** Loyalty point conversion rate for one theater: shows the effective rate (this theater's own
 * setting, or the built-in default of 1.000đ = 1 point / 100 points = 1.000đ) and lets an admin
 * change it or reset it to the default. There is no global setting. */
@Component({
  selector: 'app-theater-loyalty',
  standalone: false,
  templateUrl: './theater-loyalty.component.html',
  styleUrls: ['./theater-catalog-tab.scss', './theater-loyalty.component.scss'],
})
export class TheaterLoyaltyComponent implements OnChanges {
  @Input({ required: true }) theaterId!: string;

  form: FormGroup;
  hasOverride = false;
  loading = false;
  saving = false;
  clearing = false;

  /** Sample order used to turn the raw rates into a plain-language example under each field. */
  readonly sampleOrderAmount = 165000;
  readonly samplePoints = 100;

  /** Built-in default (mirrors LoyaltyRates on the server): 1.000đ spent = 1 point; 1 point = 10đ. */
  readonly defaultPointsPerUnit = 1000;
  readonly defaultPointValueVnd = 10;

  constructor(
    private _fb: FormBuilder,
    private _cinemaService: CinemaServiceAgent.HttpService,
    private _toast: ToastService,
    private _translate: TranslateService,
    private _cdr: ChangeDetectorRef,
  ) {
    this.form = this._fb.group({
      pointsPerUnit: [this.defaultPointsPerUnit, [Validators.required, Validators.min(1)]],
      pointValueVnd: [this.defaultPointValueVnd, [Validators.required, Validators.min(1)]],
    });
  }

  ngOnChanges(): void {
    if (this.theaterId) {
      this._load();
    }
  }

  private _load(): void {
    this.loading = true;
    this._cinemaService.getEffectiveLoyaltyRate(this.theaterId).subscribe({
      next: rate => {
        this.hasOverride = rate.isCustom;
        this.form.patchValue({ pointsPerUnit: rate.pointsPerUnit, pointValueVnd: rate.pointValueVnd });
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
      theaterId: this.theaterId,
      pointsPerUnit: this.form.value.pointsPerUnit,
      pointValueVnd: this.form.value.pointValueVnd,
    })).subscribe({
      next: () => {
        this.hasOverride = true;
        this.saving = false;
        this._toast.success(this._translate.instant('theaters.loyalty.saved'));
        this._cdr.markForCheck();
      },
      error: e => {
        this.saving = false;
        this._toast.error(this._err(e, this._translate.instant('theaters.loyalty.saveFailed')));
        this._cdr.markForCheck();
      },
    });
  }

  clearOverride(): void {
    this.clearing = true;
    this._cinemaService.resetTheaterLoyaltyToDefault(this.theaterId).subscribe({
      next: () => {
        this.hasOverride = false;
        this.clearing = false;
        this._toast.success(this._translate.instant('theaters.loyalty.cleared'));
        this._cdr.markForCheck();
        this._load();
      },
      error: e => {
        this.clearing = false;
        this._toast.error(this._err(e, this._translate.instant('theaters.loyalty.clearFailed')));
        this._cdr.markForCheck();
      },
    });
  }

  get earnExamplePoints(): number {
    const rate = Number(this.form.value.pointsPerUnit) || 0;
    if (rate <= 0) {
      return 0;
    }
    return Math.floor(this.sampleOrderAmount / rate);
  }

  get redeemExampleAmount(): number {
    const rate = Number(this.form.value.pointValueVnd) || 0;
    return this.samplePoints * rate;
  }

  private _err(e: any, fallback: string): string {
    const x = e?.error;
    return (typeof x === 'string' && x) ? x : (x?.error || x?.message || fallback);
  }
}

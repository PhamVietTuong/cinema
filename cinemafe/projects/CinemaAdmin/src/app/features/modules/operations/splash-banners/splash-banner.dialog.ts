import { ChangeDetectorRef, Component, Inject } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, ValidationErrors, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CinemaServiceAgent, showLoading, hideLoading, showSuccess, showException } from 'CinemaLib';
import { ImageUploadService } from '../../../../shared/image-upload.service';

type Dto = CinemaServiceAgent.SplashBannerDTO;

export interface SplashBannerDialogData {
  banner: Dto | null;
}

const LINK_PATTERN = /^(https?:\/\/|\/(?!\/)).*/;

function dateRangeValidator(group: AbstractControl): ValidationErrors | null {
  const start = group.get('startDate')?.value;
  const end = group.get('endDate')?.value;
  if (start && end && end < start) {
    return { dateRangeInvalid: true };
  }
  return null;
}

/** Create/edit form for a SplashBanner (CinemaUser popup), opened via MatDialog. Resolves `true`
 * on save, `false` on cancel. */
@Component({
  selector: 'app-splash-banner-dialog',
  standalone: false,
  templateUrl: './splash-banner.dialog.html',
  styleUrl: './splash-banner.dialog.scss',
})
export class SplashBannerDialog {
  readonly editingId: string | null;
  form: FormGroup;

  uploading = false;
  uploadError = '';

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _store: Store<any>,
    private _upload: ImageUploadService,
    private _cdr: ChangeDetectorRef,
    private _dialogRef: MatDialogRef<SplashBannerDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) data: SplashBannerDialogData,
  ) {
    const item = data.banner;
    this.editingId = item?.id ?? null;
    this.form = this._fb.group({
      title: [item?.title ?? '', [Validators.required, Validators.maxLength(200)]],
      imageUrl: [item?.imageUrl ?? '', Validators.required],
      linkUrl: [item?.linkUrl ?? '', Validators.pattern(LINK_PATTERN)],
      startDate: [this._toDateInput(item?.startDate), Validators.required],
      endDate: [this._toDateInput(item?.endDate), Validators.required],
      isPublished: [item?.isPublished ?? false],
    }, { validators: dateRangeValidator });
  }

  onPickImage(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) {
      return;
    }
    this.uploading = true;
    this.uploadError = '';
    this._upload.upload(file).subscribe({
      next: url => {
        this.form.patchValue({ imageUrl: url });
        this.uploading = false;
        this._cdr.markForCheck();
      },
      error: () => {
        this.uploadError = 'splashBanners.uploadFailed';
        this.uploading = false;
        this._cdr.markForCheck();
      },
    });
  }

  clearImage(): void {
    this.form.patchValue({ imageUrl: '' });
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    const payload = {
      title: v.title.trim(),
      imageUrl: v.imageUrl.trim(),
      linkUrl: v.linkUrl?.trim() || null,
      startDate: v.startDate,
      endDate: v.endDate,
      isPublished: !!v.isPublished,
    };
    const obs = this.editingId
      ? this._svc.updateSplashBanner(CinemaServiceAgent.UpdateSplashBannerRequest.fromJS({ ...payload, id: this.editingId }))
      : this._svc.createSplashBanner(CinemaServiceAgent.CreateSplashBannerRequest.fromJS(payload));

    this._store.dispatch(showLoading());
    obs.subscribe({
      next: () => {
        this._store.dispatch(showSuccess({}));
        this._dialogRef.close(true);
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }

  cancel(): void {
    this._dialogRef.close(false);
  }

  /** Builds a `yyyy-MM-dd` value for a date `<input>` from the local calendar date, not a UTC
   * round-trip (`toISOString()` shifts the date backward whenever the browser's timezone is ahead
   * of UTC, e.g. Vietnam at UTC+7, silently moving the saved window back a day). */
  private _toDateInput(value: Date | undefined): string {
    if (!value) {
      return '';
    }
    const date = new Date(value);
    const pad = (n: number) => `${n}`.padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
  }
}

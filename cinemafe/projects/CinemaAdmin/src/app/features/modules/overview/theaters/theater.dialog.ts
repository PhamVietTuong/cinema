import { ChangeDetectorRef, Component, Inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CinemaServiceAgent, showLoading, hideLoading, showSuccess, showException } from 'CinemaLib';
import { ImageUploadService } from '../../../../shared/image-upload.service';

type Dto = CinemaServiceAgent.TheaterDTO;

export interface TheaterDialogData {
  theater: Dto | null;
}

/** Create/edit form for a theater, opened via MatDialog. Resolves `true` on save, `false` on cancel. */
@Component({
  selector: 'app-theater-dialog',
  standalone: false,
  templateUrl: './theater.dialog.html',
})
export class TheaterDialog {
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
    private _dialogRef: MatDialogRef<TheaterDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) data: TheaterDialogData,
  ) {
    this.editingId = data.theater?.id ?? null;
    this.form = this._fb.group({
      name: [data.theater?.name ?? '', Validators.required],
      city: [data.theater?.city ?? '', Validators.required],
      address: [data.theater?.address ?? '', Validators.required],
      phone: [data.theater?.phone ?? '', Validators.pattern(/^(?:\+84|0)\d{9,10}$/)],
      email: ['', Validators.email],
      imageUrl: [data.theater?.imageUrl ?? ''],
    });
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
        this.uploadError = 'Tải ảnh thất bại.';
        this.uploading = false;
        this._cdr.markForCheck();
      },
    });
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.value;
    const obs = this.editingId
      ? this._svc.updateTheater(CinemaServiceAgent.UpdateTheaterRequest.fromJS({ ...v, id: this.editingId }))
      : this._svc.createTheater(CinemaServiceAgent.CreateTheaterRequest.fromJS(v));

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
}

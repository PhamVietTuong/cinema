import { ChangeDetectorRef, Component, Inject, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CinemaServiceAgent, showLoading, hideLoading, showSuccess, showException } from 'CinemaLib';
import { ImageUploadService } from '../../../../shared/image-upload.service';

type Dto = CinemaServiceAgent.MovieDTO;

export interface MovieDialogData {
  movie: Dto | null;
}

/** Create/edit form for a movie, opened via MatDialog. Resolves `true` on save, `false` on cancel. */
@Component({
  selector: 'app-movie-dialog',
  standalone: false,
  templateUrl: './movie.dialog.html',
})
export class MovieDialog implements OnInit {
  readonly editingId: string | null;
  form: FormGroup;

  ageRestrictions: CinemaServiceAgent.AgeRestrictionDTO[] = [];
  movieTypes: CinemaServiceAgent.MovieTypeDTO[] = [];

  uploading = false;
  uploadError = '';

  constructor(
    private _svc: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _store: Store<any>,
    private _upload: ImageUploadService,
    private _cdr: ChangeDetectorRef,
    private _dialogRef: MatDialogRef<MovieDialog, boolean>,
    @Inject(MAT_DIALOG_DATA) data: MovieDialogData,
  ) {
    this.editingId = data.movie?.id ?? null;
    this.form = this._fb.group({
      title: [data.movie?.title ?? '', Validators.required],
      description: [data.movie?.description ?? '', Validators.required],
      duration: [data.movie?.duration ?? 0, [Validators.required, Validators.min(1)]],
      releaseDate: [this._toDateInput(data.movie?.releaseDate), Validators.required],
      endDate: [this._toDateInput(data.movie?.endDate)],
      director: [data.movie?.director ?? ''],
      cast: [data.movie?.cast ?? ''],
      language: [data.movie?.language ?? ''],
      subtitle: [data.movie?.subtitle ?? ''],
      trailerUrl: [data.movie?.trailerUrl ?? ''],
      posterUrl: [data.movie?.posterUrl ?? ''],
      ageRestrictionId: [data.movie?.ageRestrictionId ?? '', Validators.required],
      movieTypeIds: [data.movie?.movieTypeIds ?? []],
    });
  }

  ngOnInit(): void {
    const wide = CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200 });
    this._svc.getAgeRestrictions(wide).subscribe(r => {
      this.ageRestrictions = r.results ?? [];
      this._cdr.markForCheck();
    });
    this._svc.getMovieTypes(wide).subscribe(r => {
      this.movieTypes = r.results ?? [];
      this._cdr.markForCheck();
    });
  }

  /** Formats a Date to the `yyyy-MM-dd` value an <input type="date"> requires. */
  private _toDateInput(d?: Date): string {
    if (!d) {
      return '';
    }
    const dt = new Date(d);
    const pad = (n: number) => `${n}`.padStart(2, '0');
    return `${dt.getFullYear()}-${pad(dt.getMonth() + 1)}-${pad(dt.getDate())}`;
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
        this.form.patchValue({ posterUrl: url });
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
    const payload = { ...v, endDate: v.endDate || undefined };
    const obs = this.editingId
      ? this._svc.updateMovie(CinemaServiceAgent.UpdateMovieRequest.fromJS({ ...payload, id: this.editingId }))
      : this._svc.createMovie(CinemaServiceAgent.CreateMovieRequest.fromJS(payload));

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

import { ChangeDetectorRef, Component, Inject, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { CinemaServiceAgent, showLoading, hideLoading, showSuccess, showException, LookupCacheService, toDateKey } from 'CinemaLib';
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
  styleUrl: './movie.dialog.scss',
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
      duration: [data.movie?.duration ?? 0, [Validators.required, Validators.min(1), Validators.max(600)]],
      releaseDate: [this._toDateInput(data.movie?.releaseDate), Validators.required],
      endDate: [this._toDateInput(data.movie?.endDate)],
      director: [data.movie?.director ?? ''],
      cast: [data.movie?.cast ?? ''],
      language: [data.movie?.language ?? ''],
      subtitle: [data.movie?.subtitle ?? ''],
      imdbId: [data.movie?.imdbId ?? '', Validators.pattern(/^\s*tt\d{7,10}\s*$/)],
      trailerUrl: [data.movie?.trailerUrl ?? ''],
      posterUrl: [data.movie?.posterUrl ?? ''],
      ageRestrictionId: [data.movie?.ageRestrictionId ?? '', Validators.required],
      movieTypeIds: [data.movie?.movieTypeIds ?? []],
    });
  }

  private readonly _lookup = inject(LookupCacheService);
  private readonly _i18n = inject(TranslateService);

  ngOnInit(): void {
    this._lookup.ageRestrictions().subscribe(r => {
      this.ageRestrictions = r.results ?? [];
      this._cdr.markForCheck();
    });
    this._lookup.movieTypes().subscribe(r => {
      this.movieTypes = r.results ?? [];
      this._cdr.markForCheck();
    });
  }

  /** Formats a Date to the `yyyy-MM-dd` value an <input type="date"> requires. */
  private _toDateInput(d?: Date): string {
    if (!d) {
      return '';
    }
    return toDateKey(new Date(d));
  }

  movieTypeName(id: string): string {
    return this.movieTypes.find(t => t.id === id)?.name ?? '';
  }

  removeMovieType(id: string, event: Event): void {
    event.stopPropagation();
    event.preventDefault();
    const control = this.form.controls['movieTypeIds'];
    control.setValue((control.value as string[]).filter(t => t !== id));
    control.markAsDirty();
  }

  onPickImage(event: Event): void {
    this._uploadImage(
      event,
      url => this.form.patchValue({ posterUrl: url }),
      (uploading, error) => { this.uploading = uploading; this.uploadError = error; },
    );
  }

  private _uploadImage(event: Event, onUrl: (url: string) => void, setState: (uploading: boolean, error: string) => void): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) {
      return;
    }
    setState(true, '');
    this._upload.upload(file).subscribe({
      next: url => {
        onUrl(url);
        setState(false, '');
        this._cdr.markForCheck();
      },
      error: () => {
        setState(false, 'Tải ảnh thất bại.');
        this._cdr.markForCheck();
      },
    });
  }

  save(): void {
    if (!this.form.valid) {
      this.form.markAllAsTouched();
      return;
    }
    // Credits are deliberately omitted (null = untouched on the server); they are edited on the cast page.
    const v = this.form.value;
    const payload: Record<string, unknown> = { ...v, endDate: v.endDate || undefined, imdbId: v.imdbId?.trim() || undefined };
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

  /** Candidates from the last "Find IMDb ID" lookup; null = no lookup done yet, [] = nothing found. */
  imdbMatches: CinemaServiceAgent.ImdbMatchDTO[] | null = null;
  searchingImdb = false;

  /** Looks the title (+ release year when set) up on OMDb so the admin can pick the id instead of typing it. */
  findImdbId(): void {
    const title = (this.form.value.title ?? '').trim();
    if (!title || this.searchingImdb) {
      return;
    }
    const year = this._releaseYear();
    const filters: Record<string, string> = { title };
    if (year) {
      filters['year'] = String(year);
    }
    this.searchingImdb = true;
    this._svc.searchImdbMatches(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 10, filters })).subscribe({
      next: r => {
        this.imdbMatches = r.results ?? [];
        this.searchingImdb = false;
        this._cdr.markForCheck();
      },
      error: error => {
        this.searchingImdb = false;
        this._store.dispatch(showException({ error }));
        this._cdr.markForCheck();
      },
    });
  }

  pickImdbMatch(match: CinemaServiceAgent.ImdbMatchDTO): void {
    this.form.patchValue({ imdbId: match.imdbId });
    this.form.controls['imdbId'].markAsDirty();
    this.imdbMatches = null;
  }

  private _releaseYear(): number | null {
    const raw: string = this.form.value.releaseDate ?? '';
    const year = parseInt(raw.slice(0, 4), 10);
    return Number.isFinite(year) && year > 1800 ? year : null;
  }

  /** Leaving for the cast page closes the dialog, so unsaved dialog edits need an explicit OK. */
  onManageCast(event: Event): void {
    if (this.form.dirty && !window.confirm(this._i18n.instant('movies.cast.discardDialogConfirm'))) {
      event.preventDefault();
      return;
    }
    this.cancel();
  }

  cancel(): void {
    this._dialogRef.close(false);
  }
}

import { ChangeDetectorRef, Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { TranslateService } from '@ngx-translate/core';
import { ImageUploadService } from '../../shared/image-upload.service';
import { ModalComponent } from '../../shared/modal.component';

/**
 * Self-contained create/edit form for a movie, shown in a scrim+modal popup.
 * The list page only toggles `open` and passes the row to edit; this component
 * owns the form, lookups, image upload and the create/update calls.
 */
@Component({
  selector: 'app-movie-form',
  standalone: true,
  imports: [SharedModule, ModalComponent],
  templateUrl: './movie-form.component.html',
  styleUrl: './movie-form.component.scss',
})
export class MovieFormComponent implements OnInit, OnChanges {
  @Input() open = false;
  @Input() movie: CinemaServiceAgent.MovieDTO | null = null;
  @Output() saved = new EventEmitter<void>();
  @Output() closed = new EventEmitter<void>();

  ageRestrictions: CinemaServiceAgent.AgeRestrictionDTO[] = [];
  movieTypes: CinemaServiceAgent.MovieTypeDTO[] = [];
  uploading = false;
  uploadError = '';
  form: FormGroup;

  constructor(
    private _cinemaService: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _cdr: ChangeDetectorRef,
    private _upload: ImageUploadService,
    private _translate: TranslateService,
  ) {
    this.form = this._fb.group({
      title: ['', Validators.required],
      description: ['', Validators.required],
      duration: [0, [Validators.required, Validators.min(1)]],
      releaseDate: ['', Validators.required],
      endDate: [''],
      director: [''],
      cast: [''],
      language: [''],
      subtitle: [''],
      trailerUrl: [''],
      posterUrl: [''],
      ageRestrictionId: ['', Validators.required],
      movieTypeIds: [[] as string[]],
    });
  }

  ngOnInit(): void {
    const wide = CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 500 });
    this._cinemaService.getAgeRestrictions(wide)
      .subscribe(r => { this.ageRestrictions = r.results ?? []; this._cdr.markForCheck(); });
    this._cinemaService.getMovieTypes(wide)
      .subscribe(r => { this.movieTypes = r.results ?? []; this._cdr.markForCheck(); });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open'] && this.open) {
      this._sync();
    }
  }

  get editingId(): string | null {
    return this.movie?.id ?? null;
  }

  private _sync(): void {
    this.uploadError = '';
    if (this.movie) {
      this.form.reset({ movieTypeIds: [] });
      this.form.patchValue({
        ...this.movie,
        releaseDate: this._toDateInput(this.movie.releaseDate),
        endDate: this._toDateInput(this.movie.endDate),
        ageRestrictionId: this.movie.ageRestrictionId ?? '',
        movieTypeIds: this.movie.movieTypeIds ?? [],
      } as any);
    } else {
      this.form.reset({ duration: 0, movieTypeIds: [] });
    }
  }

  /** Formats a Date to the `yyyy-MM-dd` value an <input type="date"> requires. */
  private _toDateInput(d?: Date): string {
    if (!d) { return ''; }
    const dt = new Date(d);
    const pad = (n: number) => `${n}`.padStart(2, '0');
    return `${dt.getFullYear()}-${pad(dt.getMonth() + 1)}-${pad(dt.getDate())}`;
  }

  onPickImage(e: Event): void {
    const file = (e.target as HTMLInputElement).files?.[0];
    if (!file) { return; }
    this.uploading = true; this.uploadError = '';
    this._upload.upload(file).subscribe({
      next: url => { this.form.patchValue({ posterUrl: url }); this.uploading = false; this._cdr.markForCheck(); },
      error: () => { this.uploadError = this._translate.instant('movies.form.uploadFailed'); this.uploading = false; this._cdr.markForCheck(); },
    });
  }

  save(): void {
    if (!this.form.valid) { this.form.markAllAsTouched(); return; }
    const payload = { ...this.form.value, endDate: this.form.value.endDate || undefined };
    const obs = this.editingId
      ? this._cinemaService.updateMovie(CinemaServiceAgent.UpdateMovieRequest.fromJS({ ...payload, id: this.editingId }))
      : this._cinemaService.createMovie(CinemaServiceAgent.CreateMovieRequest.fromJS(payload));
    obs.subscribe(() => { this.saved.emit(); this.closed.emit(); });
  }

  cancel(): void {
    this.closed.emit();
  }
}

import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { ImageUploadService } from '../../shared/image-upload.service';

@Component({
  selector: 'app-movies-management',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './movies-management.component.html',
  styleUrl: './movies-management.component.scss'
})
export class MoviesManagementComponent implements OnInit {
  movies: CinemaServiceAgent.MovieDTO[] = [];
  search = '';
  showForm = false;
  editingId: string | null = null;
  uploading = false;
  uploadError = '';
  form: FormGroup;

  constructor(
    private _cinemaService: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _cdr: ChangeDetectorRef,
    private _upload: ImageUploadService,
  ) {
    this.form = this._fb.group({
      title: ['', Validators.required],
      description: ['', Validators.required],
      duration: [0, [Validators.required, Validators.min(1)]],
      releaseDate: ['', Validators.required],
      director: [''],
      language: [''],
      posterUrl: [''],
    });
  }

  ngOnInit(): void { this.loadMovies(); }

  get filtered(): CinemaServiceAgent.MovieDTO[] {
    const q = this.search.trim().toLowerCase();
    if (!q) return this.movies;
    return this.movies.filter(m =>
      (m.title ?? '').toLowerCase().includes(q) ||
      (m.director ?? '').toLowerCase().includes(q));
  }

  loadMovies(): void {
    this._cinemaService.getMovies(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 100 }))
      .subscribe(r => { this.movies = r.results ?? []; this._cdr.markForCheck(); });
  }

  openCreate(): void { this.cancelEdit(); this.showForm = true; }

  editMovie(movie: CinemaServiceAgent.MovieDTO): void {
    this.editingId = movie.id ?? null;
    this.showForm = true;
    this.form.patchValue({ ...movie, releaseDate: this._toDateInput(movie.releaseDate) } as any);
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
      error: () => { this.uploadError = 'Tải ảnh thất bại.'; this.uploading = false; this._cdr.markForCheck(); },
    });
  }

  saveMovie(): void {
    if (!this.form.valid) { this.form.markAllAsTouched(); return; }
    const obs = this.editingId
      ? this._cinemaService.updateMovie(CinemaServiceAgent.UpdateMovieRequest.fromJS({ ...this.form.value, id: this.editingId }))
      : this._cinemaService.createMovie(CinemaServiceAgent.CreateMovieRequest.fromJS(this.form.value));
    obs.subscribe(() => { this.loadMovies(); this.cancelEdit(); });
  }

  deleteMovie(id?: string): void {
    if (id && confirm('Xóa phim này?')) {
      this._cinemaService.deleteMovie(id).subscribe(() => this.loadMovies());
    }
  }

  cancelEdit(): void { this.showForm = false; this.editingId = null; this.form.reset({ duration: 0 }); }
}

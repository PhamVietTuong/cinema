import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';

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
  form: FormGroup;

  constructor(
    private _cinemaService: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _cdr: ChangeDetectorRef,
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

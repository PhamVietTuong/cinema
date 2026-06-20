import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { MovieFormComponent } from './movie-form.component';
import { ConfirmModalComponent } from '../../shared/confirm-modal.component';

@Component({
  selector: 'app-movies-management',
  standalone: true,
  imports: [SharedModule, MovieFormComponent, ConfirmModalComponent],
  templateUrl: './movies-management.component.html',
  styleUrl: './movies-management.component.scss'
})
export class MoviesManagementComponent implements OnInit {
  movies: CinemaServiceAgent.MovieDTO[] = [];
  search = '';
  showForm = false;
  editing: CinemaServiceAgent.MovieDTO | null = null;

  confirmOpen = false;
  private _pendingDeleteId: string | null = null;

  constructor(
    private _cinemaService: CinemaServiceAgent.HttpService,
    private _cdr: ChangeDetectorRef,
  ) {}

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

  openCreate(): void { this.editing = null; this.showForm = true; }

  editMovie(movie: CinemaServiceAgent.MovieDTO): void { this.editing = movie; this.showForm = true; }

  onSaved(): void { this.loadMovies(); }

  deleteMovie(id?: string): void {
    if (!id) { return; }
    this._pendingDeleteId = id;
    this.confirmOpen = true;
  }

  confirmDelete(): void {
    const id = this._pendingDeleteId;
    this.confirmOpen = false;
    this._pendingDeleteId = null;
    if (id) { this._cinemaService.deleteMovie(id).subscribe(() => this.loadMovies()); }
  }
}

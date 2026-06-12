import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';

type Dto = CinemaServiceAgent.MovieTypeDetailDTO;

@Component({
  selector: 'app-movie-type-details',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './movie-type-details.component.html',
})
export class MovieTypeDetailsManagementComponent implements OnInit {
  private _svc = inject(CinemaServiceAgent.HttpService);
  private _cdr = inject(ChangeDetectorRef);
  private _fb = inject(FormBuilder);

  items: Dto[] = [];
  movies: CinemaServiceAgent.MovieDTO[] = [];
  movieTypes: CinemaServiceAgent.MovieTypeDTO[] = [];
  totalCount = 0;
  pageIndex = 1;
  pageSize = 10;
  readonly pageSizeOptions = [5, 10, 20, 50];
  showForm = false;
  form = this._fb.group({
    movieId: ['', Validators.required],
    movieTypeId: ['', Validators.required],
  });

  ngOnInit(): void {
    this.load();
    this._svc.getMovies(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 500 }))
      .subscribe(r => { this.movies = r.results ?? []; this._cdr.markForCheck(); });
    this._svc.getMovieTypes(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 500 }))
      .subscribe(r => { this.movieTypes = r.results ?? []; this._cdr.markForCheck(); });
  }

  load(): void {
    this._svc.getMovieTypeDetails(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: this.pageIndex, pageSize: this.pageSize }))
      .subscribe(r => { this.items = r.results ?? []; this.totalCount = r.totalCount ?? 0; this._cdr.markForCheck(); });
  }

  openCreate(): void { this.showForm = true; this.form.reset(); }
  cancelEdit(): void { this.showForm = false; this.form.reset(); }

  save(): void {
    if (!this.form.valid) { this.form.markAllAsTouched(); return; }
    this._svc.createMovieTypeDetail(CinemaServiceAgent.CreateMovieTypeDetailRequest.fromJS(this.form.value))
      .subscribe({ next: () => { this.load(); this.cancelEdit(); } });
  }

  delete(movieId?: string, movieTypeId?: string): void {
    if (movieId && movieTypeId && confirm('Xóa liên kết phim – thể loại này?')) {
      this._svc.deleteMovieTypeDetail(movieId, movieTypeId).subscribe({ next: () => this.load() });
    }
  }

  get totalPages(): number { return Math.max(1, Math.ceil(this.totalCount / this.pageSize)); }
  get rangeStart(): number { return this.totalCount === 0 ? 0 : (this.pageIndex - 1) * this.pageSize + 1; }
  get rangeEnd(): number { return Math.min(this.pageIndex * this.pageSize, this.totalCount); }
  goToPage(p: number): void { const t = Math.min(Math.max(1, p), this.totalPages); if (t !== this.pageIndex) { this.pageIndex = t; this.load(); } }
  prevPage(): void { this.goToPage(this.pageIndex - 1); }
  nextPage(): void { this.goToPage(this.pageIndex + 1); }
  changePageSize(s: number): void { this.pageSize = +s; this.pageIndex = 1; this.load(); }
}

import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { CinemaServiceAgent } from 'CinemaLib';

@Component({
  selector: 'app-theaters-management',
  standalone: false,
  templateUrl: './theaters-management.component.html',
  styleUrl: './theaters-management.component.scss'
})
export class TheatersManagementComponent implements OnInit {
  theaters: CinemaServiceAgent.TheaterDTO[] = [];
  search = '';
  showForm = false;
  editing: CinemaServiceAgent.TheaterDTO | null = null;

  constructor(
    private _cinemaService: CinemaServiceAgent.HttpService,
    private _cdr: ChangeDetectorRef,
    private _router: Router,
  ) {}

  ngOnInit(): void { this.loadTheaters(); }

  get filtered(): CinemaServiceAgent.TheaterDTO[] {
    const q = this.search.trim().toLowerCase();
    if (!q) return this.theaters;
    return this.theaters.filter(t =>
      (t.name ?? '').toLowerCase().includes(q) ||
      (t.city ?? '').toLowerCase().includes(q));
  }

  loadTheaters(): void {
    this._cinemaService.getTheaters(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 100 }))
      .subscribe(t => { this.theaters = t.results ?? []; this._cdr.markForCheck(); });
  }

  openCreate(): void { this.editing = null; this.showForm = true; }

  openDetail(t: CinemaServiceAgent.TheaterDTO): void { this._router.navigate(['/theaters', t.id]); }

  onSaved(): void { this.loadTheaters(); }
}

import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';

@Component({
  selector: 'app-theaters',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './theaters.component.html',
  styleUrl: './theaters.component.scss',
})
export class TheatersComponent implements OnInit {
  private _cinema = inject(CinemaServiceAgent.HttpService);
  private _cdr = inject(ChangeDetectorRef);

  theaters: CinemaServiceAgent.TheaterDTO[] = [];
  city = '';
  loading = true;

  ngOnInit(): void {
    this._cinema.getTheaters(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 100 }))
      .subscribe({
        next: r => { this.theaters = r.results ?? []; this.loading = false; this._cdr.markForCheck(); },
        error: () => { this.loading = false; this._cdr.markForCheck(); },
      });
  }

  get cities(): string[] {
    return [...new Set(this.theaters.map(t => t.city ?? '').filter(Boolean))];
  }
  get filtered(): CinemaServiceAgent.TheaterDTO[] {
    return this.city ? this.theaters.filter(t => t.city === this.city) : this.theaters;
  }
}

import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';

@Component({
  selector: 'app-promotions',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './promotions.component.html',
  styleUrl: './promotions.component.scss',
})
export class PromotionsComponent implements OnInit {
  private _cinema = inject(CinemaServiceAgent.HttpService);
  private _cdr = inject(ChangeDetectorRef);

  promos: CinemaServiceAgent.DiscountDTO[] = [];
  loading = true;

  ngOnInit(): void {
    this._cinema.getDiscounts(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 100 }))
      .subscribe({
        next: r => { this.promos = (r.results ?? []).filter(p => p.isActive); this.loading = false; this._cdr.markForCheck(); },
        error: () => { this.loading = false; this._cdr.markForCheck(); },
      });
  }
}

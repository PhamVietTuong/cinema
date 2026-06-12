import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';

@Component({
  selector: 'app-membership',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './membership.component.html',
  styleUrl: './membership.component.scss',
})
export class MembershipComponent implements OnInit {
  private _cinema = inject(CinemaServiceAgent.HttpService);
  private _cdr = inject(ChangeDetectorRef);

  tiers: CinemaServiceAgent.MemberShipDTO[] = [];
  loading = true;

  readonly perks = [
    'Tích điểm trên mỗi vé đã mua',
    'Giảm giá vé theo hạng thành viên',
    'Ưu đãi sinh nhật & sự kiện độc quyền',
    'Đặt vé sớm cho các suất công chiếu',
  ];

  ngOnInit(): void {
    this._cinema.getMemberShips(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 50 }))
      .subscribe({
        next: r => {
          this.tiers = (r.results ?? []).slice().sort((a, b) => (a.minPoints ?? 0) - (b.minPoints ?? 0));
          this.loading = false;
          this._cdr.markForCheck();
        },
        error: () => { this.loading = false; this._cdr.markForCheck(); },
      });
  }

  /** The middle tier is visually highlighted as "popular". */
  isFeatured(i: number): boolean {
    return this.tiers.length > 2 && i === Math.floor(this.tiers.length / 2);
  }
}

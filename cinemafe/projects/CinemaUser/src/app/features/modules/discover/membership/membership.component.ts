import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { SharedModule, CinemaServiceAgent, selectCurrentUser } from 'CinemaLib';
import { CountUpDirective } from '../../../../shared/count-up.directive';

@Component({
  selector: 'app-membership',
  standalone: true,
  imports: [SharedModule, CountUpDirective],
  templateUrl: './membership.component.html',
  styleUrl: './membership.component.scss',
})
export class MembershipComponent implements OnInit {
  private _cinema = inject(CinemaServiceAgent.HttpService);
  private _cdr = inject(ChangeDetectorRef);

  private _store = inject(Store);

  readonly user$ = this._store.select(selectCurrentUser);
  tiers: CinemaServiceAgent.MemberShipDTO[] = [];
  loading = true;

  readonly perks = [
    'membership.perkEarnPoints',
    'membership.perkBirthday',
    'membership.perkEarlyBooking',
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

  /** Index of the tier the points currently fall in (-1 when below the first tier). */
  currentTierIndex(points: number): number {
    let idx = -1;
    this.tiers.forEach((t, i) => {
      if ((t.minPoints ?? 0) <= points) {
        idx = i;
      }
    });
    return idx;
  }

  /** The tier after the current one, or null at the top. */
  nextTier(points: number): CinemaServiceAgent.MemberShipDTO | null {
    return this.tiers[this.currentTierIndex(points) + 1] ?? null;
  }

  /** How far through the current tier's span the points are, 0-100 (100 at the top tier). */
  progressPercent(points: number): number {
    const next = this.nextTier(points);
    if (!next) {
      return 100;
    }
    const cur = this.tiers[this.currentTierIndex(points)];
    const from = cur?.minPoints ?? 0;
    const span = (next.minPoints ?? 0) - from;
    return span > 0 ? Math.max(0, Math.min(100, ((points - from) / span) * 100)) : 0;
  }
}

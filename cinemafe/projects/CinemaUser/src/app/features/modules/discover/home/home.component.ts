import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { Store } from '@ngrx/store';
import {
  SharedModule, CinemaServiceAgent, DialogService, PromotionEligibilityValues,
  loadNowShowing, loadComingSoon,
  selectNowShowing, selectComingSoon, selectMoviesLoading,
} from 'CinemaLib';
import { MovieCardComponent } from '../../../../shared/movie-card/movie-card.component';
import { SiteFooterComponent } from '../../../../shared/site-footer/site-footer.component';
import { ScrollRowComponent } from '../../../../shared/scroll-row/scroll-row.component';
import { RevealOnScrollDirective } from '../../../../shared/reveal-on-scroll.directive';
import { movieDetailCommands } from '../../../../shared/movie-route';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [SharedModule, MovieCardComponent, SiteFooterComponent, ScrollRowComponent, RevealOnScrollDirective],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
})
export class HomeComponent implements OnInit {
  private _cinema = inject(CinemaServiceAgent.HttpService);
  private _cdr = inject(ChangeDetectorRef);
  private _dialogs = inject(DialogService);

  readonly movieDetailCommands = movieDetailCommands;
  /** Decorative only — there is a single featured slide, so the dots never advance. */
  readonly heroDots = [0, 1, 2, 3];

  nowShowing$: Observable<any[]>;
  comingSoon$: Observable<any[]>;
  loading$: Observable<boolean>;
  featuredMovie$: Observable<any>;

  /** Personalised picks (top-rated for anonymous visitors). */
  recommended: any[] = [];
  promotions: CinemaServiceAgent.PromotionProgramDTO[] = [];
  tiers: CinemaServiceAgent.MemberShipDTO[] = [];

  constructor(private _store: Store) {
    this.nowShowing$ = this._store.select(selectNowShowing);
    this.comingSoon$ = this._store.select(selectComingSoon);
    this.loading$ = this._store.select(selectMoviesLoading);
    this.featuredMovie$ = this.nowShowing$.pipe(map(movies => movies?.[0] ?? null));
  }

  ngOnInit(): void {
    this._store.dispatch(loadNowShowing({ page: 1, pageSize: 50 }));
    this._store.dispatch(loadComingSoon({ page: 1, pageSize: 50 }));
    this._cinema.getRecommendedMovies(8).subscribe({
      next: r => { this.recommended = r ?? []; this._cdr.markForCheck(); },
      error: () => this._cdr.markForCheck(),
    });
    this._cinema.getPublishedPromotionPrograms(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 8 }))
      .subscribe({
        next: r => { this.promotions = r.results ?? []; this._cdr.markForCheck(); },
        error: () => this._cdr.markForCheck(),
      });
    this._cinema.getMemberShips(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 50 }))
      .subscribe({
        next: r => {
          this.tiers = (r.results ?? []).slice().sort((a, b) => (a.minPoints ?? 0) - (b.minPoints ?? 0));
          this._cdr.markForCheck();
        },
        error: () => this._cdr.markForCheck(),
      });
  }

  /** Opens the trailer dialog for the hero's featured movie. */
  openTrailer(movie: any): void {
    if (!movie?.trailerUrl) {
      return;
    }
    this._dialogs.openTrailerDialog({ title: movie.title, trailerUrl: movie.trailerUrl });
  }

  eligibilityLabel(p: CinemaServiceAgent.PromotionProgramDTO): string {
    return PromotionEligibilityValues.find(x => x.value === p.eligibility)?.name ?? '';
  }

  /** The highest tier gets the pulsing-glow chip. */
  isTopTier(i: number): boolean {
    return i === this.tiers.length - 1;
  }

  /** Falls back to the bundled placeholder when a poster URL fails to load. */
  onImgError(e: Event): void {
    const img = e.target as HTMLImageElement;
    if (!img.src.endsWith('assets/no-poster.jpg')) {
      img.src = 'assets/no-poster.jpg';
    }
  }
}

import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { SharedModule, selectCurrentUser } from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';

/** Placeholder landing page: greets the user and shows which theater the staff tools are scoped to. */
@Component({
  selector: 'staff-home',
  standalone: true,
  imports: [SharedModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="home">
      <div class="ad-card home-card">
        <mat-icon class="home-icon">waving_hand</mat-icon>
        <h1>{{ 'home.welcome' | translate: { name: user()?.name ?? '' } }}</h1>
        @if (theater.currentTheaterId()) {
          <p>{{ 'home.theater' | translate: { name: theater.currentTheaterName() ?? '' } }}</p>
        } @else if (theater.isAdmin()) {
          <p>{{ 'home.pickTheater' | translate }}</p>
        } @else {
          <p>{{ 'home.noTheater' | translate }}</p>
        }
        <p class="hint">{{ 'home.comingSoon' | translate }}</p>
      </div>
    </div>
  `,
  styles: [`
    .home { padding: 32px; display: flex; justify-content: center; }
    .home-card { max-width: 520px; width: 100%; text-align: center; padding: 40px 32px; }
    .home-icon { font-size: 44px; width: 44px; height: 44px; color: var(--ml-action-strong); }
    h1 { font-family: var(--ml-font-head); text-transform: uppercase; font-size: 1.35rem; margin: 16px 0 8px; color: var(--ml-ink); }
    p { color: var(--ml-ink); margin: 0 0 8px; }
    .hint { color: var(--ml-muted); }
  `],
})
export class HomeComponent {
  readonly theater = inject(TheaterContextService);
  readonly user = inject(Store).selectSignal(selectCurrentUser);
}

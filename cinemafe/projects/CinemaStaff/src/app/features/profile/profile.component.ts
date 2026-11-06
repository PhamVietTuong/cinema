import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Store } from '@ngrx/store';
import { catchError, of } from 'rxjs';
import { CinemaServiceAgent, SharedModule, selectCurrentUser, showException } from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';

/** Staff profile: account summary. */
@Component({
  selector: 'staff-profile',
  standalone: true,
  imports: [SharedModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
})
export class ProfileComponent {
  private readonly _store = inject(Store);
  private readonly _cinema = inject(CinemaServiceAgent.HttpService);
  private readonly _theaterContext = inject(TheaterContextService);
  private readonly _destroyRef = inject(DestroyRef);

  readonly user = this._store.selectSignal(selectCurrentUser);
  private readonly _theater = signal<CinemaServiceAgent.TheaterDTO | null>(null);
  private readonly _loadingTheater = signal(false);
  readonly theater = this._theater.asReadonly();
  readonly loadingTheater = this._loadingTheater.asReadonly();
  readonly theaterLocation = computed(() => {
    const t = this._theater();
    return t ? [t.address, t.city].filter(part => !!part).join(', ') : '';
  });

  constructor() {
    // Mirrors TheaterContextService's own id resolution (own theaterId for staff, picked theater for Admin).
    effect(() => {
      const theaterId = this._theaterContext.currentTheaterId();
      if (!theaterId) {
        this._theater.set(null);
        return;
      }
      this._loadingTheater.set(true);
      this._cinema.getTheater(theaterId)
        .pipe(
          catchError(error => {
            this._store.dispatch(showException({ error }));
            return of(null);
          }),
          takeUntilDestroyed(this._destroyRef),
        )
        .subscribe(theater => {
          this._theater.set(theater);
          this._loadingTheater.set(false);
        });
    });
  }
}

import { ChangeDetectionStrategy, Component, Input, inject } from '@angular/core';
import { SharedModule } from 'CinemaLib';
import { WatchlistService } from './watchlist.service';

/** Heart button that saves a movie to the local "watch later" list. */
@Component({
  selector: 'app-watch-toggle',
  standalone: true,
  imports: [SharedModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button type="button" class="wt-btn" [class.is-saved]="saved()" [class.wt-btn--hero]="variant === 'hero'"
            [attr.aria-pressed]="saved()"
            [attr.aria-label]="(saved() ? 'ux.watchlist.remove' : 'ux.watchlist.add') | translate"
            [title]="(saved() ? 'ux.watchlist.remove' : 'ux.watchlist.add') | translate"
            (click)="onClick($event)">
      <mat-icon>{{ saved() ? 'favorite' : 'favorite_border' }}</mat-icon>
    </button>
  `,
  styleUrl: './watch-toggle.component.scss',
})
export class WatchToggleComponent {
  private readonly _watchlist = inject(WatchlistService);

  @Input({ required: true }) movieId = '';
  /** 'card' floats over a poster; 'hero' is a larger standalone button next to the hero actions. */
  @Input() variant: 'card' | 'hero' = 'card';

  // Reads the service signal, so the OnPush template re-renders when the list changes.
  saved(): boolean {
    return this._watchlist.has(this.movieId);
  }

  onClick(event: Event): void {
    // Sits on top of clickable posters/links: never let the click activate them.
    event.stopPropagation();
    event.preventDefault();
    if (this.movieId) {
      this._watchlist.toggle(this.movieId);
    }
  }
}

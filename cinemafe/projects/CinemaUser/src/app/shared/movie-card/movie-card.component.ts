import { Component, Input, inject } from '@angular/core';
import { SharedModule, DialogService } from 'CinemaLib';
import { movieDetailCommands } from '../movie-route';
import { RevealOnScrollDirective } from '../reveal-on-scroll.directive';

/**
 * One movie poster card, shared by Home's carousels/grid and the Movies list —
 * previously copy-pasted markup+styles in both places.
 */
@Component({
  selector: 'app-movie-card',
  standalone: true,
  imports: [SharedModule, RevealOnScrollDirective],
  templateUrl: './movie-card.component.html',
  styleUrl: './movie-card.component.scss',
})
export class MovieCardComponent {
  private _dialogs = inject(DialogService);

  @Input({ required: true }) movie: any;
  /** 'showing' renders the rating badge + book-on-hover overlay + clicks through to detail;
   *  'coming' renders a "coming soon" badge + release date instead, and is not clickable. */
  @Input() variant: 'showing' | 'coming' = 'showing';
  /** Fixed 234px width for a horizontal carousel row vs. fluid width in a grid. */
  @Input() fixedWidth = false;
  /**
   * Home's slider look: age/genre corner badges, a dedicated Trailer button and a Book Tickets
   * (or Notify Me, for 'coming') button, instead of the rating badge + book-on-hover overlay. The
   * Movies list page leaves this false so its cards are unchanged.
   */
  @Input() actions = false;

  get detailCommands(): string[] {
    return movieDetailCommands(this.movie.slug);
  }

  get hasTrailer(): boolean {
    return !!this.movie?.trailerUrl;
  }

  /** Opens the trailer dialog for this card's movie; no-ops (and leaves any routerLink click alone) without one. */
  openTrailer(event?: Event): void {
    if (!this.hasTrailer) {
      return;
    }
    event?.stopPropagation();
    event?.preventDefault();
    this._dialogs.openTrailerDialog({ title: this.movie.title, trailerUrl: this.movie.trailerUrl });
  }

  /** Falls back to the bundled placeholder when a poster URL fails to load. */
  onImgError(e: Event): void {
    const img = e.target as HTMLImageElement;
    if (!img.src.endsWith('assets/no-poster.jpg')) {
      img.src = 'assets/no-poster.jpg';
    }
  }
}

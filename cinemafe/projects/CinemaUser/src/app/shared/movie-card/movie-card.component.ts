import { Component, Input } from '@angular/core';
import { SharedModule } from 'CinemaLib';
import { movieDetailCommands } from '../movie-route';

/**
 * One movie poster card, shared by Home's carousels/grid and the Movies list —
 * previously copy-pasted markup+styles in both places.
 */
@Component({
  selector: 'app-movie-card',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './movie-card.component.html',
  styleUrl: './movie-card.component.scss',
})
export class MovieCardComponent {
  @Input({ required: true }) movie: any;
  /** 'showing' renders the rating badge + book-on-hover overlay + clicks through to detail;
   *  'coming' renders a "coming soon" badge + release date instead, and is not clickable. */
  @Input() variant: 'showing' | 'coming' = 'showing';
  /** Fixed 234px width for a horizontal carousel row vs. fluid width in a grid. */
  @Input() fixedWidth = false;

  get detailCommands(): string[] {
    return movieDetailCommands(this.movie.slug);
  }

  /** Falls back to the bundled placeholder when a poster URL fails to load. */
  onImgError(e: Event): void {
    const img = e.target as HTMLImageElement;
    if (!img.src.endsWith('assets/no-poster.jpg')) {
      img.src = 'assets/no-poster.jpg';
    }
  }
}

import { Component, ElementRef, ViewChild } from '@angular/core';
import { SharedModule } from 'CinemaLib';

/**
 * Horizontal scroll track with left/right chevron buttons, shared by Home's Recommended/Now
 * Showing/Coming Soon/Promotions rows so the slider chrome (scroll track + buttons + the one-time
 * hint pulse) exists once instead of being copied into four template blocks.
 */
@Component({
  selector: 'app-scroll-row',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './scroll-row.component.html',
  styleUrl: './scroll-row.component.scss',
})
export class ScrollRowComponent {
  @ViewChild('track', { static: true }) track!: ElementRef<HTMLElement>;

  private static readonly STEP_PX = 340;

  scroll(dir: -1 | 1): void {
    this.track.nativeElement.scrollBy({ left: dir * ScrollRowComponent.STEP_PX, behavior: 'smooth' });
  }
}

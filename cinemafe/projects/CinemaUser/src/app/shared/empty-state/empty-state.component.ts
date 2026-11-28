import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { SharedModule } from 'CinemaLib';

/**
 * Illustrated empty state: a themed SVG badge around a Material icon, a title, a hint and an
 * optional call to action — a router link when `ctaLink` is set, otherwise a button that emits
 * `cta`. Text comes in as translate keys (`hintText` is for verbatim server messages).
 */
@Component({
  selector: 'app-empty-state',
  standalone: true,
  imports: [SharedModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './empty-state.component.html',
  styleUrl: './empty-state.component.scss',
})
export class EmptyStateComponent {
  @Input() icon = 'movie';
  @Input({ required: true }) titleKey = '';
  @Input() hintKey = '';
  @Input() hintText = '';
  @Input() ctaKey = '';
  @Input() ctaLink: string | null = null;
  @Output() cta = new EventEmitter<void>();
}

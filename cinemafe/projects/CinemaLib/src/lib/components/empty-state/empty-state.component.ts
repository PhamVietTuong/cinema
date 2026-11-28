import { ChangeDetectionStrategy, Component, booleanAttribute, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * "Nothing here" placeholder for lists and panels — the single empty state for every app.
 *
 * Plain (default): icon + message + optional hint, for tables and panels —
 * `<cl-empty-state icon="inventory_2" messageKey="inventory.empty" hintKey="..." />`.
 *
 * `illustrated`: a themed SVG badge around the icon with a title, for whole-page states (customer app). It can also
 * carry a call to action: a router link when `ctaLink` is set, otherwise a button that emits `cta`.
 * `hintText` is for verbatim server messages; everything else is an i18n key.
 */
@Component({
  selector: 'cl-empty-state',
  standalone: true,
  imports: [MatIconModule, MatButtonModule, RouterLink, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './empty-state.component.html',
  styleUrl: './empty-state.component.scss',
})
export class EmptyStateComponent {
  /** i18n key of the main message (the title in the illustrated variant). */
  readonly messageKey = input.required<string>();
  /** Optional i18n key of a smaller second line. */
  readonly hintKey = input<string | null>(null);
  /** Optional verbatim second line (e.g. a server error); wins over `hintKey`. */
  readonly hintText = input<string | null>(null);
  /** Material icon name. */
  readonly icon = input<string>('inventory_2');
  /** Whole-page variant with the SVG badge. */
  readonly illustrated = input(false, { transform: booleanAttribute });
  /** i18n key of the call-to-action label; no button when empty. */
  readonly ctaKey = input<string | null>(null);
  /** Router link for the call to action; when empty the button emits `cta` instead. */
  readonly ctaLink = input<string | null>(null);
  /** Emitted when the call-to-action button (no `ctaLink`) is pressed. */
  readonly cta = output<void>();
}

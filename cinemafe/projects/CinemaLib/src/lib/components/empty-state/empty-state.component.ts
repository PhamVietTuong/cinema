import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * "Nothing here" placeholder for lists and panels.
 *
 * Usage: `<cl-empty-state icon="inventory_2" messageKey="inventory.empty" hintKey="..." />`
 */
@Component({
  selector: 'cl-empty-state',
  standalone: true,
  imports: [MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="ad-empty">
      <mat-icon>{{ icon() }}</mat-icon>
      <p>{{ messageKey() | translate }}</p>
      @if (hintKey()) {
        <p class="cl-empty-hint">{{ hintKey()! | translate }}</p>
      }
    </div>
  `,
  styles: [`
    :host { display: block; }
    .cl-empty-hint { font-size: 12px; }
  `],
})
export class EmptyStateComponent {
  /** i18n key of the main message. */
  readonly messageKey = input.required<string>();
  /** Optional i18n key of a smaller second line. */
  readonly hintKey = input<string | null>(null);
  /** Material icon name. */
  readonly icon = input<string>('inventory_2');
}

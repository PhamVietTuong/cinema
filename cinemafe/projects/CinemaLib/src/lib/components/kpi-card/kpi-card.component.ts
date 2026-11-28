import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/** `stub`: ticket-stub tile with an icon (dashboards). `default`: label over a large figure. `compact`: dense figure for report pages. */
export type KpiCardVariant = 'default' | 'stub' | 'compact';

/**
 * One headline figure: `<cl-kpi-card [label]="'dashboard.kpiMovies' | translate" [value]="stats.movies" icon="movie" variant="stub" />`.
 * The caller formats the value (`| vnd`, `| number`, `| percent`) and translates the label; lay tiles out in the page's own grid.
 */
@Component({
  selector: 'cl-kpi-card',
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './kpi-card.component.html',
  styleUrl: './kpi-card.component.scss',
})
export class KpiCardComponent {
  /** Already-translated caption. */
  readonly label = input.required<string>();
  /** Already-formatted figure. */
  readonly value = input.required<string | number | null | undefined>();
  /** Optional Material icon shown beside the label (`stub` variant). */
  readonly icon = input<string | null>(null);
  readonly variant = input<KpiCardVariant>('default');
}

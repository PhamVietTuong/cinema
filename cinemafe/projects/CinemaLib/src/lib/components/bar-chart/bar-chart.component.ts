import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** One bar of a `cl-bar-chart`. */
export interface BarChartRow {
  label: string;
  value: number;
  /** Text shown at the end of the row; defaults to the plain value. */
  valueLabel?: string;
}

/** Width of a bar as a percentage (0 to 100) of `max`; a non-positive max or value gives 0. */
export function barPercent(value: number, max: number): number {
  if (!(max > 0) || !(value > 0)) {
    return 0;
  }
  return Math.min(100, (value / max) * 100);
}

/**
 * Plain-CSS horizontal bar chart (the same pattern as the admin revenue report, no chart library).
 * By default the longest bar is the largest value; pass `max` for a fixed scale such as 1 for rates.
 *
 * Usage: `<cl-bar-chart [rows]="rows" />`
 */
@Component({
  selector: 'cl-bar-chart',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './bar-chart.component.html',
  styleUrl: './bar-chart.component.scss',
})
export class BarChartComponent {
  readonly rows = input.required<readonly BarChartRow[]>();
  /** Value that fills the whole track; omitted = the largest row value. */
  readonly max = input<number | null>(null);

  /** Hides the row names, for a bar used inside a table cell. */
  readonly compact = input<boolean>(false);

  private readonly _scale = computed(() => this.max() ?? Math.max(0, ...this.rows().map(row => row.value)));

  protected percent(value: number): number {
    return barPercent(value, this._scale());
  }
}

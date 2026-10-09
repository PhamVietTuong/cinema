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
  template: `
    <div class="cl-bars">
      @for (row of rows(); track $index) {
        <div class="cl-bar-row" [class.cl-bar-row--compact]="compact()">
          @if (!compact()) {
            <span class="cl-bar-name" [title]="row.label">{{ row.label }}</span>
          }
          <span class="cl-bar-track"><span class="cl-bar-fill" [style.width.%]="percent(row.value)"></span></span>
          <span class="cl-bar-val">{{ row.valueLabel ?? row.value }}</span>
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: block; }
    .cl-bars { display: flex; flex-direction: column; gap: 8px; }
    .cl-bar-row { display: grid; grid-template-columns: minmax(80px, 200px) 1fr minmax(70px, auto); gap: 12px; align-items: center; }
    .cl-bar-row--compact { grid-template-columns: 1fr minmax(48px, auto); }
    .cl-bar-name { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .cl-bar-track { height: 10px; border-radius: 5px; background: var(--ml-panel-3, rgba(0, 0, 0, 0.08)); overflow: hidden; }
    .cl-bar-fill { display: block; height: 100%; border-radius: 5px; background: var(--ml-action, #6750a4); }
    .cl-bar-val { text-align: right; font-variant-numeric: tabular-nums; }
  `],
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

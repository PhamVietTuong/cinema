import { Directive, ElementRef, Input, OnChanges, OnDestroy, Renderer2, inject } from '@angular/core';

/**
 * Counts a number up from 0 to `appCountUp` when it first renders. Writes textContent through
 * Renderer2 (no bindings, zoneless-safe). Reduced-motion visitors, and non-numeric values, get the
 * final value straight away.
 */
@Directive({
  selector: '[appCountUp]',
  standalone: true,
})
export class CountUpDirective implements OnChanges, OnDestroy {
  private readonly _el = inject(ElementRef<HTMLElement>);
  private readonly _renderer = inject(Renderer2);
  private _raf = 0;
  private _current: number | null = null;

  @Input('appCountUp') value: number | null | undefined = 0;
  @Input() decimals = 0;
  @Input() durationMs = 900;
  /** Tween from the last shown value instead of from 0 (use for totals that change as the user edits). */
  @Input() fromPrevious = false;
  /** Thousands separators (en-US, matching Angular's number pipe) for whole-number money. */
  @Input() group = false;
  @Input() suffix = '';

  ngOnChanges(): void {
    cancelAnimationFrame(this._raf);
    const target = Number(this.value ?? 0);
    const start = this.fromPrevious && this._current !== null ? this._current : 0;
    const write = (n: number) => {
      this._current = n;
      const text = this.group ? n.toLocaleString('en-US', { minimumFractionDigits: this.decimals, maximumFractionDigits: this.decimals }) : n.toFixed(this.decimals);
      this._renderer.setProperty(this._el.nativeElement, 'textContent', text + this.suffix);
    };

    if (!Number.isFinite(target) || window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      write(Number.isFinite(target) ? target : 0);
      return;
    }

    if (start === target) {
      write(target);
      return;
    }

    const t0 = performance.now();
    const tick = (now: number) => {
      const p = Math.min((now - t0) / this.durationMs, 1);
      write(start + (target - start) * (1 - Math.pow(1 - p, 3)));
      if (p < 1) {
        this._raf = requestAnimationFrame(tick);
      }
    };
    this._raf = requestAnimationFrame(tick);
  }

  ngOnDestroy(): void {
    cancelAnimationFrame(this._raf);
  }
}

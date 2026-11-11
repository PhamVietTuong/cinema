import { Directive, ElementRef, OnDestroy, inject } from '@angular/core';

/**
 * Click-and-drag panning for any horizontally scrolling container (timetable boards, price matrices,
 * wide tables): `<div class="board" cinemaDragScroll>`. Mouse only: touch and pen keep their native
 * panning, wheel / shift+wheel / keyboard are untouched. Panning starts after a 5px move so plain clicks on
 * buttons inside the container still work, and the click that ends a drag is swallowed so it does not
 * open the block the pointer was released over.
 */
@Directive({
  selector: '[cinemaDragScroll]',
  standalone: true,
  host: { class: 'cl-drag-scroll' },
})
export class DragScrollDirective implements OnDestroy {
  private static readonly THRESHOLD_PX = 5;

  private readonly _el = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private _pointerId: number | null = null;
  private _startX = 0;
  private _startY = 0;
  private _startLeft = 0;
  private _startTop = 0;
  private active = false;

  private readonly _down = (e: PointerEvent): void => {
    const el = this._el;
    if (e.pointerType !== 'mouse' || e.button !== 0 || (el.scrollWidth <= el.clientWidth && el.scrollHeight <= el.clientHeight)) {
      return;
    }
    // Leave text fields, selects and the native scrollbar alone. Measured against the container, not the
    // event target (offsetX is relative to the target, which is often a wide child of the scroller).
    const target = e.target as HTMLElement;
    const box = el.getBoundingClientRect();
    if (
      target.closest('input, textarea, select, [contenteditable="true"]') ||
      e.clientX > box.left + el.clientLeft + el.clientWidth ||
      e.clientY > box.top + el.clientTop + el.clientHeight
    ) {
      return;
    }
    this._refreshCan();
    this._pointerId = e.pointerId;
    this._startX = e.clientX;
    this._startY = e.clientY;
    this._startLeft = el.scrollLeft;
    this._startTop = el.scrollTop;
  };

  private readonly _move = (e: PointerEvent): void => {
    if (this._pointerId !== e.pointerId) {
      return;
    }
    const el = this._el;
    if ((e.buttons & 1) === 0) {
      // Released outside the container before the drag began: nothing is held any more.
      this._reset();
      return;
    }
    const dx = e.clientX - this._startX;
    const dy = e.clientY - this._startY;
    if (!this.active && Math.abs(dx) + Math.abs(dy) > DragScrollDirective.THRESHOLD_PX) {
      this.active = true;
      el.setPointerCapture(e.pointerId);
      el.classList.add('cl-drag-scroll--active');
    }
    if (this.active) {
      el.scrollLeft = this._startLeft - dx;
      // Vertical panning only when this container scrolls vertically itself (the page keeps its own scroll).
      if (el.scrollHeight > el.clientHeight) {
        el.scrollTop = this._startTop - dy;
      }
    }
  };

  /** The grab cursor is only offered while there is something to pan (mouse hovering a container that overflows). */
  private readonly _enter = (e: PointerEvent): void => {
    if (e.pointerType !== 'mouse') {
      return;
    }
    this._refreshCan();
  };

  private _refreshCan(): void {
    const el = this._el;
    el.classList.toggle('cl-drag-scroll--can', el.scrollWidth > el.clientWidth || el.scrollHeight > el.clientHeight);
  }

  /** Native HTML5 drag of an <img>/<a> inside the container would cancel the pan, so refuse it while a press is pending. */
  private readonly _dragStart = (e: Event): void => {
    if (this._pointerId !== null) {
      e.preventDefault();
    }
  };

  private _reset(): void {
    const el = this._el;
    if (this._pointerId !== null && el.hasPointerCapture(this._pointerId)) {
      el.releasePointerCapture(this._pointerId);
    }
    this._pointerId = null;
    this.active = false;
    el.classList.remove('cl-drag-scroll--active');
  }

  private readonly _lostCapture = (): void => {
    this._reset();
  };

  private readonly _end = (e: PointerEvent): void => {
    if (this._pointerId !== e.pointerId) {
      return;
    }
    const el = this._el;
    this._pointerId = null;
    if (this.active) {
      const swallow = (ev: Event): void => {
        ev.stopPropagation();
        ev.preventDefault();
      };
      el.addEventListener('click', swallow, { capture: true, once: true });
      setTimeout(() => el.removeEventListener('click', swallow, true));
      if (el.hasPointerCapture(e.pointerId)) {
        el.releasePointerCapture(e.pointerId);
      }
    }
    this.active = false;
    el.classList.remove('cl-drag-scroll--active');
  };

  constructor() {
    const el = this._el;
    el.addEventListener('pointerenter', this._enter);
    el.addEventListener('pointerdown', this._down);
    el.addEventListener('pointermove', this._move);
    el.addEventListener('pointerup', this._end);
    el.addEventListener('pointercancel', this._end);
    el.addEventListener('lostpointercapture', this._lostCapture);
    el.addEventListener('dragstart', this._dragStart);
  }

  ngOnDestroy(): void {
    const el = this._el;
    this._reset();
    el.removeEventListener('lostpointercapture', this._lostCapture);
    el.removeEventListener('dragstart', this._dragStart);
    el.removeEventListener('pointerenter', this._enter);
    el.removeEventListener('pointerdown', this._down);
    el.removeEventListener('pointermove', this._move);
    el.removeEventListener('pointerup', this._end);
    el.removeEventListener('pointercancel', this._end);
  }
}

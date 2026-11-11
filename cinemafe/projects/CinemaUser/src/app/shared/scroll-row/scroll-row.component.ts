import { AfterViewInit, Component, ElementRef, OnDestroy, ViewChild, signal } from '@angular/core';
import { SharedModule } from 'CinemaLib';

/**
 * Horizontal scroll track shared by Home rows, Promotions and Movie-detail cast/crew. It is operable
 * with every input: prev/next chevrons (fine pointers, only when the content overflows, disabled at the
 * ends), shift+wheel / horizontal wheel / touch swipe (native), click-drag with the mouse, and the
 * arrow/Home/End keys when the track is focused. A plain vertical wheel is never intercepted, so the
 * page keeps scrolling over the row.
 */
@Component({
  selector: 'app-scroll-row',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './scroll-row.component.html',
  styleUrl: './scroll-row.component.scss',
})
export class ScrollRowComponent implements AfterViewInit, OnDestroy {
  @ViewChild('track', { static: true }) track!: ElementRef<HTMLElement>;

  readonly overflowing = signal(false);
  readonly canPrev = signal(false);
  readonly canNext = signal(false);
  readonly dragging = signal(false);

  private static readonly STEP_PX = 340;
  private static readonly DRAG_THRESHOLD_PX = 5;

  private resizeObserver?: ResizeObserver;
  private mutationObserver?: MutationObserver;
  private readonly onLoad = (): void => this.update();
  private pointerId: number | null = null;
  private startX = 0;
  private startScroll = 0;
  private moved = false;
  private readonly swallowClick = (e: Event): void => {
    e.preventDefault();
    e.stopPropagation();
  };

  ngAfterViewInit(): void {
    const el = this.track.nativeElement;
    this.update();
    if (typeof ResizeObserver !== 'undefined') {
      this.resizeObserver = new ResizeObserver(() => this.update());
      this.resizeObserver.observe(el);
    }
    if (typeof MutationObserver !== 'undefined') {
      // Projected items are direct children of the track; re-measure when they are added or removed.
      this.mutationObserver = new MutationObserver(() => this.update());
      this.mutationObserver.observe(el, { childList: true });
    }
    // Images finishing loading change scrollWidth without resizing the track.
    el.addEventListener('load', this.onLoad, true);
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    this.mutationObserver?.disconnect();
    this.track.nativeElement.removeEventListener('load', this.onLoad, true);
  }

  update(): void {
    const el = this.track.nativeElement;
    const max = el.scrollWidth - el.clientWidth;
    this.overflowing.set(max > 1);
    this.canPrev.set(el.scrollLeft > 1);
    this.canNext.set(el.scrollLeft < max - 1);
  }

  scroll(dir: -1 | 1): void {
    const el = this.track.nativeElement;
    const step = Math.max(ScrollRowComponent.STEP_PX, Math.round(el.clientWidth * 0.8));
    el.scrollBy({ left: dir * Math.min(step, ScrollRowComponent.STEP_PX * 2), behavior: 'smooth' });
  }

  onKeydown(e: KeyboardEvent): void {
    const el = this.track.nativeElement;
    if (e.key === 'ArrowRight') {
      this.scroll(1);
    } else if (e.key === 'ArrowLeft') {
      this.scroll(-1);
    } else if (e.key === 'Home') {
      el.scrollTo({ left: 0, behavior: 'smooth' });
    } else if (e.key === 'End') {
      el.scrollTo({ left: el.scrollWidth, behavior: 'smooth' });
    } else {
      return;
    }
    e.preventDefault();
  }

  // Mouse drag only; touch and pen keep native panning.
  onPointerDown(e: PointerEvent): void {
    if (e.pointerType !== 'mouse' || e.button !== 0 || !this.overflowing()) {
      return;
    }
    this.pointerId = e.pointerId;
    this.startX = e.clientX;
    this.startScroll = this.track.nativeElement.scrollLeft;
    this.moved = false;
  }

  onPointerMove(e: PointerEvent): void {
    if (this.pointerId !== e.pointerId) {
      return;
    }
    const el = this.track.nativeElement;
    const dx = e.clientX - this.startX;
    if (!this.moved && Math.abs(dx) > ScrollRowComponent.DRAG_THRESHOLD_PX) {
      this.moved = true;
      this.dragging.set(true);
      el.setPointerCapture(e.pointerId);
    }
    if (this.moved) {
      el.scrollLeft = this.startScroll - dx;
    }
  }

  onPointerEnd(e: PointerEvent): void {
    if (this.pointerId !== e.pointerId) {
      return;
    }
    const el = this.track.nativeElement;
    this.pointerId = null;
    if (this.moved) {
      // The drag must not activate the card or link the pointer is released over.
      el.addEventListener('click', this.swallowClick, { capture: true, once: true });
      setTimeout(() => el.removeEventListener('click', this.swallowClick, true));
      if (el.hasPointerCapture(e.pointerId)) {
        el.releasePointerCapture(e.pointerId);
      }
    }
    this.moved = false;
    this.dragging.set(false);
  }
}

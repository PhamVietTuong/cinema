import { Directive, ElementRef, OnDestroy, OnInit, inject } from '@angular/core';

/**
 * Hero poster motion: it drifts slower than the page while scrolling (parallax) and tilts in 3D toward
 * the pointer. Writes the transform straight to the element (no bindings - the app is zoneless) and is a
 * no-op for reduced-motion visitors; the tilt only runs on devices that really hover.
 */
@Directive({
  selector: '[appHeroMotion]',
  standalone: true,
})
export class HeroMotionDirective implements OnInit, OnDestroy {
  private readonly _el = inject<ElementRef<HTMLElement>>(ElementRef);
  private _rx = 0;
  private _ry = 0;
  private _py = 0;
  private _raf = 0;
  private readonly _onScroll = () => {
    this._py = Math.min(window.scrollY, 600) * 0.12;
    this._schedule();
  };
  private readonly _onMove = (e: PointerEvent) => {
    const r = this._el.nativeElement.getBoundingClientRect();
    this._ry = ((e.clientX - r.left) / r.width - 0.5) * 10;
    this._rx = -((e.clientY - r.top) / r.height - 0.5) * 10;
    this._schedule();
  };
  private readonly _onLeave = () => {
    this._rx = 0;
    this._ry = 0;
    this._schedule();
  };

  ngOnInit(): void {
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      return;
    }
    const host = this._el.nativeElement;
    host.style.transition = 'transform 0.18s ease-out';
    host.style.willChange = 'transform';
    window.addEventListener('scroll', this._onScroll, { passive: true });
    if (window.matchMedia('(hover: hover)').matches) {
      host.addEventListener('pointermove', this._onMove);
      host.addEventListener('pointerleave', this._onLeave);
    }
  }

  ngOnDestroy(): void {
    cancelAnimationFrame(this._raf);
    window.removeEventListener('scroll', this._onScroll);
    this._el.nativeElement.removeEventListener('pointermove', this._onMove);
    this._el.nativeElement.removeEventListener('pointerleave', this._onLeave);
  }

  private _schedule(): void {
    cancelAnimationFrame(this._raf);
    this._raf = requestAnimationFrame(() => {
      this._el.nativeElement.style.transform =
        `perspective(900px) translate3d(0, ${this._py}px, 0) rotateX(${this._rx}deg) rotateY(${this._ry}deg)`;
    });
  }
}

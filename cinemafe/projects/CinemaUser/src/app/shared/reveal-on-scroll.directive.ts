import { Directive, ElementRef, OnDestroy, OnInit, Renderer2, inject } from '@angular/core';

/**
 * Fades a card in with a slight rise the first time it scrolls into the viewport. Toggles the
 * `is-revealed` class directly on the host via Renderer2 (no binding, no change detection) since the
 * app is zoneless — see `.is-revealed` in movie-card/home SCSS for the actual transition.
 */
@Directive({
  selector: '[appRevealOnScroll]',
  standalone: true,
})
export class RevealOnScrollDirective implements OnInit, OnDestroy {
  private readonly _el = inject(ElementRef<HTMLElement>);
  private readonly _renderer = inject(Renderer2);
  private _observer: IntersectionObserver | null = null;

  ngOnInit(): void {
    const host = this._el.nativeElement;

    if (!('IntersectionObserver' in window)) {
      this._renderer.addClass(host, 'is-revealed');
      return;
    }

    this._observer = new IntersectionObserver((entries) => {
      for (const entry of entries) {
        if (entry.isIntersecting) {
          this._renderer.addClass(host, 'is-revealed');
          this._observer?.unobserve(host);
        }
      }
    }, { threshold: 0.15 });
    this._observer.observe(host);
  }

  ngOnDestroy(): void {
    this._observer?.disconnect();
  }
}

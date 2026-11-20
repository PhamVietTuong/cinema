import { Directive, ElementRef, Input, OnChanges, inject } from '@angular/core';

/**
 * Tints the closest `.hero` with the poster's dominant colour (`--hero-glow`, a registered <color>, so
 * it cross-fades when the hero changes movie). Posters are usually served from third-party hosts without
 * CORS, which taints the canvas; in that case it falls back to a stable hue derived from the title.
 */
@Directive({
  selector: '[appAmbientGlow]',
  standalone: true,
})
export class AmbientGlowDirective implements OnChanges {
  private readonly _el = inject<ElementRef<HTMLElement>>(ElementRef);

  @Input('appAmbientGlow') posterUrl: string | null | undefined = '';
  @Input() glowSeed = '';

  ngOnChanges(): void {
    const target = this._el.nativeElement.closest<HTMLElement>('.hero');
    if (!target) {
      return;
    }
    const set = (color: string) => target.style.setProperty('--hero-glow', color);
    set(this._fallback());

    if (!this.posterUrl) {
      return;
    }
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => {
      try {
        const size = 16;
        const canvas = document.createElement('canvas');
        canvas.width = size;
        canvas.height = size;
        const ctx = canvas.getContext('2d');
        if (!ctx) {
          return;
        }
        ctx.drawImage(img, 0, 0, size, size);
        const data = ctx.getImageData(0, 0, size, size).data;
        let r = 0;
        let g = 0;
        let b = 0;
        let w = 0;
        for (let i = 0; i < data.length; i += 4) {
          const max = Math.max(data[i], data[i + 1], data[i + 2]);
          const min = Math.min(data[i], data[i + 1], data[i + 2]);
          // Favour saturated, mid-brightness pixels so a white or black border does not win.
          const weight = (max - min) / 255 + 0.05;
          if (max > 235 && min > 225) {
            continue;
          }
          r += data[i] * weight;
          g += data[i + 1] * weight;
          b += data[i + 2] * weight;
          w += weight;
        }
        if (w > 0) {
          set(`rgb(${Math.round(r / w)} ${Math.round(g / w)} ${Math.round(b / w)})`);
        }
      } catch {
        // Tainted canvas: keep the title-derived fallback.
      }
    };
    img.src = this.posterUrl;
  }

  private _fallback(): string {
    let h = 0;
    for (const ch of this.glowSeed || 'cinema') {
      h = (h * 31 + ch.charCodeAt(0)) % 360;
    }
    return `hsl(${h} 65% 55%)`;
  }
}

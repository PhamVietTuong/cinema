import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslatePipe } from '@ngx-translate/core';

/** Distance scrolled (px) before the button appears. */
const SHOW_AFTER_PX = 480;

/**
 * Floating "back to top" button. The page scrolls on the window (the sidenav container is only a
 * bounded shell), so it listens to window scroll. It sits just above the chatbot FAB — both are
 * fixed bottom-right and lifted by the booking order bar height (`--cx-order-bar-h`).
 */
@Component({
  selector: 'app-back-to-top',
  standalone: true,
  // Deliberately not SharedModule: this is in the root shell, whose initial bundle is budgeted.
  imports: [MatIconModule, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button type="button" class="btt" [class.is-visible]="visible()"
            [attr.aria-label]="'ux.backToTop' | translate" [title]="'ux.backToTop' | translate"
            [attr.tabindex]="visible() ? 0 : -1" [attr.aria-hidden]="!visible()"
            (click)="toTop()">
      <mat-icon>keyboard_arrow_up</mat-icon>
    </button>
  `,
  styleUrl: './back-to-top.component.scss',
})
export class BackToTopComponent implements OnInit {
  private readonly _destroyRef = inject(DestroyRef);
  readonly visible = signal(false);

  ngOnInit(): void {
    const onScroll = (): void => {
      const show = window.scrollY > SHOW_AFTER_PX;
      if (show !== this.visible()) {
        this.visible.set(show);
      }
    };
    window.addEventListener('scroll', onScroll, { passive: true });
    this._destroyRef.onDestroy(() => window.removeEventListener('scroll', onScroll));
    onScroll();
  }

  toTop(): void {
    const reduce = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;
    window.scrollTo({ top: 0, behavior: reduce ? 'auto' : 'smooth' });
  }
}

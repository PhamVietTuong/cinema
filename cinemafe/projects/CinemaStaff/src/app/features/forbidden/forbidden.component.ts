import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { SharedModule, logout } from 'CinemaLib';

/**
 * Landing page for an authenticated user without a staff role. `roleGuard` needs a target that is
 * itself unguarded — redirecting to '/' would run the guards again and loop.
 */
@Component({
  selector: 'staff-forbidden',
  standalone: true,
  imports: [SharedModule],
  template: `
    <div class="fb-wrap">
      <div class="ad-card fb-card">
        <mat-icon class="fb-icon">block</mat-icon>
        <h1>{{ 'forbidden.title' | translate }}</h1>
        <p>{{ 'forbidden.message' | translate }}</p>
        <button type="button" class="ad-btn ad-btn--primary" (click)="signOut()">
          {{ 'forbidden.switchAccount' | translate }}
        </button>
      </div>
    </div>
  `,
  styles: [`
    .fb-wrap { min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 24px; background: var(--ml-paper); }
    .fb-card { max-width: 420px; text-align: center; padding: 40px 32px; }
    .fb-icon { font-size: 48px; width: 48px; height: 48px; color: var(--ml-danger); }
    h1 { font-family: var(--ml-font-head); text-transform: uppercase; font-size: 1.35rem; margin: 16px 0 8px; color: var(--ml-ink); }
    p { color: var(--ml-muted); margin: 0 0 24px; }
  `],
})
export class ForbiddenComponent {
  private readonly _store = inject(Store);
  private readonly _router = inject(Router);

  signOut(): void {
    this._store.dispatch(logout());
    this._router.navigate(['/auth/login']);
  }
}

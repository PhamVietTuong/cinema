import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { SharedModule, logout } from 'CinemaLib';

/**
 * Landing page for an authenticated user who is not an Admin.
 *
 * The admin app has no non-admin surface, so `adminGuard` needs a target that is
 * itself unguarded — redirecting to '/' would bounce back into /dashboard and loop.
 */
@Component({
  selector: 'app-forbidden',
  standalone: true,
  imports: [SharedModule],
  template: `
    <div class="fb-wrap">
      <div class="fb-card">
        <span class="material-icons fb-icon">block</span>
        <h1>{{ 'forbidden.title' | translate }}</h1>
        <p>{{ 'forbidden.message' | translate }}</p>
        <button type="button" class="fb-btn" (click)="signOut()">
          {{ 'forbidden.switchAccount' | translate }}
        </button>
      </div>
    </div>
  `,
  styles: [`
    .fb-wrap { min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 24px; }
    .fb-card { max-width: 420px; text-align: center; padding: 40px 32px; border-radius: 16px;
               background: var(--ad-card, #fff); border: 1px solid var(--ad-line, #e5e7eb); }
    .fb-icon { font-size: 48px; color: #dc2626; }
    h1 { font-size: 1.35rem; margin: 16px 0 8px; }
    p { color: var(--ad-muted, #6b7280); margin: 0 0 24px; }
    .fb-btn { padding: 10px 20px; border-radius: 8px; border: 0; cursor: pointer;
              background: #2563eb; color: #fff; font-weight: 600; }
  `],
})
export class ForbiddenComponent {
  private _store = inject(Store);
  private _router = inject(Router);

  signOut(): void {
    this._store.dispatch(logout());
    this._router.navigate(['/auth/login']);
  }
}

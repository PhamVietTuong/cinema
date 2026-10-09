import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { SharedModule } from '../../shared.module';
import { logout } from '../../store/auth/auth.actions';

/** An extra link shown below the standard message, e.g. "Open the Staff app". */
export interface ForbiddenExtraLink {
  href: string;
  /** i18n key of the link text. */
  labelKey: string;
  /** Material icon name shown before the text. */
  icon?: string;
}

/**
 * Landing page for an authenticated user who lacks the role the app requires.
 * Usage: `<cl-forbidden [extraLink]="staffLink()" />`
 */
@Component({
  selector: 'cl-forbidden',
  standalone: true,
  imports: [SharedModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './forbidden-page.component.html',
  styleUrl: './forbidden-page.component.scss',
})
export class ForbiddenPageComponent {
  private readonly _store = inject(Store);
  private readonly _router = inject(Router);

  readonly extraLink = input<ForbiddenExtraLink | null>(null);
  /** Route the sign-out button lands on. */
  readonly loginRoute = input<string>('/auth/login');

  signOut(): void {
    this._store.dispatch(logout());
    this._router.navigate([this.loginRoute()]);
  }
}

import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { BreakpointObserver } from '@angular/cdk/layout';
import { NavigationEnd, Router } from '@angular/router';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { filter, map, startWith } from 'rxjs/operators';
import { SharedModule } from '../../shared.module';
import { ThemeService } from '../../theme/theme.service';
import { NavItem, NavSection, filterNavByRole } from '../../models/nav.models';
import { UserProfile } from '../../models/auth.models';

/** Sidebar becomes an off-canvas mat-sidenav drawer below this width; matches the shell stylesheet's own breakpoint. */
const MOBILE_QUERY = '(max-width: 768px)';

/** Wordmark and logo shown at the top of the sidebar. Rendered as `{name}<strong>{strong}</strong>`. */
export interface ShellBrand {
  name: string;
  strong?: string;
  /** Material icon in the brand mark. Defaults to `movie_filter`. */
  icon?: string;
  /** Image URL; replaces the icon when set. */
  logoUrl?: string;
}

/** What to show when the signed-in user has no name / email yet. */
export interface ShellUserFallback {
  nameKey: string;
  email: string;
  initial: string;
}

/**
 * The back-office chrome shared by CinemaAdmin and CinemaStaff: a dark sidebar built from a
 * role-filtered menu, a topbar (page title, language + theme switch, user avatar), and a
 * profile / logout footer. Pages render through the default `<ng-content>`.
 *
 * Projection slots: `[topbarExtra]` (before the language switcher, e.g. a theater picker) and
 * `[sidebarFooter]` (above the profile card).
 */
@Component({
  selector: 'cl-app-shell',
  standalone: true,
  imports: [SharedModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app-shell.component.html',
  styleUrl: './app-shell.component.scss',
})
export class AppShellComponent {
  readonly brand = input.required<ShellBrand>();
  /** Full menu; the shell hides what the signed-in user's role may not see. */
  readonly menu = input.required<readonly NavSection[]>();
  readonly user = input<UserProfile | null | undefined>(null);
  readonly userFallback = input<ShellUserFallback>({ nameKey: 'shell.userFallback', email: '', initial: '?' });
  /** Router link of the profile card; null renders the card without a link. */
  readonly profileRoute = input<string | null>('/profile');
  /** Topbar title key when the URL matches no menu item. */
  readonly defaultTitleKey = input<string>('shell.defaultTitle');
  readonly showNotifications = input<boolean>(true);
  readonly logout = output<void>();

  private readonly _breakpoints = inject(BreakpointObserver);
  private readonly _router = inject(Router);

  /** Dark/light state — persisted and applied to <html data-theme> by the service. */
  readonly theme = inject(ThemeService);

  /** Mobile sidebar drawer open state (ignored on desktop where the rail is static). */
  readonly menuOpen = signal(false);

  /** Below the mobile breakpoint the sidenav becomes an off-canvas 'over' drawer; above it, a static 'side' rail. */
  readonly isMobile = toSignal(
    this._breakpoints.observe(MOBILE_QUERY).pipe(map(result => result.matches)),
    { initialValue: this._breakpoints.isMatched(MOBILE_QUERY) },
  );

  private readonly _url = toSignal(
    this._router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      map(() => this._router.url),
      startWith(this._router.url),
    ),
    { initialValue: this._router.url },
  );

  readonly sections = computed(() => filterNavByRole(this.menu(), this.user()?.userTypeName));

  /** Topbar title key: the menu item owning the URL's first segment, the first item for the root, else the default. */
  readonly pageTitleKey = computed(() => {
    const segment = this._url().split('?')[0].split('/').filter(Boolean)[0];
    const items: NavItem[] = this.sections().flatMap(section => section.items);
    const match = segment
      ? items.find(item => item.route.split('/').filter(Boolean)[0] === segment)
      : items[0];
    return match?.titleKey ?? this.defaultTitleKey();
  });

  constructor() {
    // Close the mobile drawer whenever navigation completes.
    this._router.events.pipe(filter(e => e instanceof NavigationEnd), takeUntilDestroyed()).subscribe(() => {
      this.menuOpen.set(false);
    });
  }

  toggleMenu(): void {
    this.menuOpen.update(open => !open);
  }

  /** mat-sidenav emits this on any open/close (backdrop click, escape key, or our own toggle). */
  onSidenavOpenedChange(opened: boolean): void {
    if (this.isMobile()) {
      this.menuOpen.set(opened);
    }
  }

  toggleTheme(): void {
    this.theme.toggle();
  }
}

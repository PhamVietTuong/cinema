import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideMockStore } from '@ngrx/store/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { SharedModule } from 'CinemaLib';
import { firstValueFrom } from 'rxjs';
// App is declared (standalone: false) in AppModule. Importing the module puts it in the
// compilation graph so the AOT compiler can resolve the template's scope (Material, RouterOutlet,
// the translate pipe); without it every element in app.html fails to resolve at build time.
import './app.module';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      // App is declared (standalone: false) in app.module, so it is declared here too.
      declarations: [App],
      imports: [NoopAnimationsModule, SharedModule],
      providers: [
        provideRouter([]),
        provideMockStore(),
        // Loader-free on purpose: the app's provideCinemaTranslation() fetches
        // /assets/i18n/*.json over HTTP, which has no place in a unit test.
        provideTranslateService({ fallbackLang: 'vi' }),
      ],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('exposes the auth observables', () => {
    const app = TestBed.createComponent(App).componentInstance;
    expect(app.isAuth$).toBeDefined();
    expect(app.user$).toBeDefined();
  });

  describe('page title', () => {
    // The title is an i18n key resolved by the translate pipe, so it must map the route
    // segment — not a literal string — or every admin page renders the same heading.
    const titleFor = async (url: string) => {
      const router = TestBed.inject(Router);
      vi.spyOn(router, 'url', 'get').mockReturnValue(url);
      const app = TestBed.createComponent(App).componentInstance;
      return firstValueFrom(app.pageTitleKey$);
    };

    it('maps a known segment to its key', async () => {
      await expect(titleFor('/movies')).resolves.toBe('pageTitle.movies');
    });

    it('maps a hyphenated segment', async () => {
      await expect(titleFor('/age-restrictions')).resolves.toBe('pageTitle.ageRestrictions');
    });

    it('ignores query params and deeper segments', async () => {
      await expect(titleFor('/theaters/abc-123?tab=rooms')).resolves.toBe('pageTitle.theaters');
    });

    it('treats the root as the dashboard', async () => {
      await expect(titleFor('/')).resolves.toBe('pageTitle.dashboard');
    });

    it('falls back for an unmapped segment', async () => {
      await expect(titleFor('/something-else')).resolves.toBe('pageTitle.default');
    });
  });

  describe('menu', () => {
    it('toggles and closes', () => {
      const app = TestBed.createComponent(App).componentInstance;

      expect(app.menuOpen).toBe(false);
      app.toggleMenu();
      expect(app.menuOpen).toBe(true);
      app.closeMenu();
      expect(app.menuOpen).toBe(false);
    });
  });
});

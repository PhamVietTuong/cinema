import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideMockStore } from '@ngrx/store/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { SharedModule, AppShellComponent } from 'CinemaLib';
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
      imports: [NoopAnimationsModule, SharedModule, AppShellComponent],
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
    expect(app.isAdmin$).toBeDefined();
    expect(app.user$).toBeDefined();
  });
});

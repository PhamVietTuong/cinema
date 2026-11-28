import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideMockStore } from '@ngrx/store/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { CinemaServiceAgent, SharedModule } from 'CinemaLib';
import { App } from './app';
import { ChatbotComponent } from './shared/chatbot/chatbot.component';
import { SearchBoxComponent } from './shared/search-box/search-box.component';
import { BackToTopComponent } from './shared/back-to-top/back-to-top.component';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [App],
      // ChatbotComponent is standalone and used by the App template; app.module imports it (and the search box / back-to-top) too.
      imports: [NoopAnimationsModule, SharedModule, ChatbotComponent, SearchBoxComponent, BackToTopComponent],
      providers: [
        provideRouter([]),
        provideMockStore(),
        // The template uses SharedModule's TranslatePipe, which needs TranslateService. Provided
        // without a loader on purpose: the app's provideCinemaTranslation() fetches
        // /assets/i18n/*.json over HTTP, which has no place in a unit test. Missing keys fall
        // through to the key itself.
        provideTranslateService({ fallbackLang: 'vi' }),
        // SplashBannerService (started from App.ngOnInit) calls getActiveSplashBanner() — stub it
        // rather than hitting a real backend.
        { provide: CinemaServiceAgent.HttpService, useValue: { getActiveSplashBanner: () => of([]) } },
      ],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should expose isAuth$ and user$ observables', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app.isAuth$).toBeDefined();
    expect(app.user$).toBeDefined();
  });
});

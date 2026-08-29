import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideMockStore } from '@ngrx/store/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { SharedModule } from 'CinemaLib';
import { App } from './app';
import { ChatbotComponent } from './shared/chatbot/chatbot.component';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [App],
      // ChatbotComponent is standalone and used by the App template; app.module imports it too.
      imports: [NoopAnimationsModule, SharedModule, ChatbotComponent],
      providers: [
        provideRouter([]),
        provideMockStore(),
        // The template uses SharedModule's TranslatePipe, which needs TranslateService. Provided
        // without a loader on purpose: the app's provideCinemaTranslation() fetches
        // /assets/i18n/*.json over HTTP, which has no place in a unit test. Missing keys fall
        // through to the key itself.
        provideTranslateService({ fallbackLang: 'vi' }),
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

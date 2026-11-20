import { NgModule, inject, provideAppInitializer } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { RouterModule, ViewTransitionInfo, provideRouter, withViewTransitions } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { StoreModule } from '@ngrx/store';
import { EffectsModule } from '@ngrx/effects';
import { StoreDevtoolsModule } from '@ngrx/store-devtools';
import {
  CinemaLibModule,
  authReducer, moviesReducer,
  AuthEffects, MoviesEffects,
  authInterceptor, errorInterceptor,
  API_BASE_URL, HUB_BASE_URL,
  CinemaServiceAgent, IdentityServiceAgent, PaymentServiceAgent,
  provideCinemaTranslation,
  ThemeService, APP_DEFAULT_THEME,
  provideCinemaSvgIcons,
} from 'CinemaLib';
import { environment } from '../environments/environment';
import { routes } from './app.routes';
import { App } from './app';
import { ChatbotComponent } from './shared/chatbot/chatbot.component';

@NgModule({
  declarations: [App],
  imports: [
    BrowserModule,
    BrowserAnimationsModule,
    RouterModule,
    CinemaLibModule,
    ChatbotComponent,
    StoreModule.forRoot({ auth: authReducer, movies: moviesReducer }),
    EffectsModule.forRoot([AuthEffects, MoviesEffects]),
    StoreDevtoolsModule.instrument({ maxAge: 25, logOnly: environment.production }),
  ],
  providers: [
    provideRouter(routes, withViewTransitions({
      // Poster -> detail hero morph. Only runs when a movie card was clicked (it is marked in App) and
      // motion is allowed; every other navigation keeps the normal page-in animation.
      skipInitialTransition: true,
      onViewTransitionCreated: ({ transition }: ViewTransitionInfo) => {
        const root = document.documentElement;
        const marked = document.querySelector<HTMLElement>('[data-vt-poster]');
        if (!marked || window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
          transition.skipTransition();
          return;
        }
        root.classList.add('vt-active');
        transition.finished.finally(() => {
          root.classList.remove('vt-active');
          marked.removeAttribute('data-vt-poster');
          marked.style.viewTransitionName = '';
        });
      },
    })),
    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
    provideCinemaTranslation(),
    ...provideCinemaSvgIcons(),
    provideAppInitializer(() => inject(ThemeService).init()),
    // "Marquee Ledger" is a light-first identity — light is the default here,
    // and the navbar toggle switches to the "After Hours" counterpart.
    { provide: APP_DEFAULT_THEME, useValue: 'light' },
    { provide: API_BASE_URL, useValue: environment.apiUrl },
    { provide: HUB_BASE_URL, useValue: environment.hubUrl },
    { provide: CinemaServiceAgent.CINEMA_BASE_URL, useValue: environment.apiUrl },
    { provide: IdentityServiceAgent.IDENTITY_BASE_URL, useValue: environment.apiUrl },
    { provide: PaymentServiceAgent.PAYMENT_BASE_URL, useValue: environment.apiUrl },
    CinemaServiceAgent.HttpService,
    IdentityServiceAgent.HttpService,
    PaymentServiceAgent.HttpService,
  ],
  bootstrap: [App],
})
export class AppModule {}

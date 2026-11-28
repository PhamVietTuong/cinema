import { NgModule, inject, provideAppInitializer } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { provideNativeDateAdapter } from '@angular/material/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { RouteReuseStrategy, RouterModule } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { StoreModule } from '@ngrx/store';
import { EffectsModule } from '@ngrx/effects';
import { provideStoreDevtools } from '@ngrx/store-devtools';
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
import { CinemaRouteReuseStrategy } from './route-reuse';
import { SearchBoxComponent } from './shared/search-box/search-box.component';
import { BackToTopComponent } from './shared/back-to-top/back-to-top.component';

@NgModule({
  declarations: [App],
  imports: [
    BrowserModule,
    RouterModule.forRoot(routes, { scrollPositionRestoration: 'enabled' }),
    CinemaLibModule,
    SearchBoxComponent,
    BackToTopComponent,
    StoreModule.forRoot({ auth: authReducer, movies: moviesReducer }),
    EffectsModule.forRoot([AuthEffects, MoviesEffects]),
  ],
  providers: [
    { provide: RouteReuseStrategy, useClass: CinemaRouteReuseStrategy },
    // Animations engine loads lazily (a separate chunk) instead of in the initial bundle.
    provideAnimationsAsync(),
    // Devtools instrumentation only in dev builds — it keeps 25 state snapshots in memory.
    ...(environment.production ? [] : [provideStoreDevtools({ maxAge: 25 })]),
    // The root no longer pulls the whole SharedModule (and its MatNativeDateModule) in, so provide the date adapter
    // that dialogs opened from root-level services and the lazy datepickers rely on.
    provideNativeDateAdapter(),
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

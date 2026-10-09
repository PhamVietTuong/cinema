import { NgModule, inject, provideAppInitializer } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { RouterModule } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { StoreModule } from '@ngrx/store';
import { EffectsModule } from '@ngrx/effects';
import { StoreDevtoolsModule } from '@ngrx/store-devtools';
import {
  CinemaLibModule,
  authReducer, moviesReducer, searchReducer, loadingReducer,
  AuthEffects, MoviesEffects, NotificationEffects,
  authInterceptor, errorInterceptor,
  API_BASE_URL, HUB_BASE_URL,
  CinemaServiceAgent, IdentityServiceAgent, PaymentServiceAgent,
  provideCinemaSvgIcons,
  provideCinemaTranslation,
  ThemeService,
} from 'CinemaLib';
import { environment } from '../environments/environment';
import { routes } from './app.routes';
import { App } from './app';

@NgModule({
  declarations: [App],
  imports: [
    BrowserModule,
    BrowserAnimationsModule,
    RouterModule.forRoot(routes),
    CinemaLibModule,
    StoreModule.forRoot({ auth: authReducer, movies: moviesReducer, searchState: searchReducer, loading: loadingReducer }),
    EffectsModule.forRoot([AuthEffects, MoviesEffects, NotificationEffects]),
    StoreDevtoolsModule.instrument({ maxAge: 25, logOnly: environment.production }),
  ],
  providers: [
    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
    provideCinemaTranslation(),
    provideAppInitializer(() => inject(ThemeService).init()),
    ...provideCinemaSvgIcons(),
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

import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { RouterModule } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { StoreModule } from '@ngrx/store';
import { EffectsModule } from '@ngrx/effects';
import { StoreDevtoolsModule } from '@ngrx/store-devtools';
import {
  SharedModule,
  authReducer, moviesReducer,
  AuthEffects, MoviesEffects,
  authInterceptor, errorInterceptor,
  API_BASE_URL, HUB_BASE_URL,
  CinemaServiceAgent, IdentityServiceAgent, PaymentServiceAgent,
  provideCinemaTranslation,
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
    RouterModule.forRoot(routes),
    SharedModule,
    ChatbotComponent,
    StoreModule.forRoot({ auth: authReducer, movies: moviesReducer }),
    EffectsModule.forRoot([AuthEffects, MoviesEffects]),
    StoreDevtoolsModule.instrument({ maxAge: 25, logOnly: environment.production }),
  ],
  providers: [
    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
    provideCinemaTranslation(),
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

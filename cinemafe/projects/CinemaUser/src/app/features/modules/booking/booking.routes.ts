import { Routes } from '@angular/router';

/** Seat selection, checkout and the payment-gateway return page. Guarded by authGuard in app.routes.ts. */
export const BOOKING_ROUTES: Routes = [
  {
    path: 'seats',
    loadComponent: () => import('./booking-page/booking-page.component').then(m => m.BookingPageComponent)
  },
  {
    path: 'checkout',
    loadComponent: () => import('./booking-checkout/booking-checkout.component').then(m => m.BookingCheckoutComponent)
  },
  {
    path: 'payment-return',
    loadComponent: () => import('./payment-return/payment-return.component').then(m => m.PaymentReturnComponent)
  }
];

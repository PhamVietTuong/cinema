import { of } from 'rxjs';
import { BookingCheckoutComponent } from './booking-checkout.component';
import { BookingCheckoutState } from './booking-checkout.state';

describe('BookingCheckoutComponent', () => {
  let router: { navigate: ReturnType<typeof vi.fn> };
  let payment: { createBooking: ReturnType<typeof vi.fn>; validateGiftCard: ReturnType<typeof vi.fn>; initiatePayment: ReturnType<typeof vi.fn>; confirmPayment: ReturnType<typeof vi.fn> };
  let identity: { getProfile: ReturnType<typeof vi.fn> };
  let hub: { connectionId: string; stopConnection: ReturnType<typeof vi.fn> };
  let cdr: { markForCheck: ReturnType<typeof vi.fn> };

  const state: BookingCheckoutState = {
    showTimeId: 'st-1',
    roomId: 'room-1',
    seats: [
      { seatId: 's1', label: 'A1', seatTypeName: 'Standard', basePrice: 100000, price: 100000, patronCategoryId: 'cat-adult', patronCategoryName: 'Adult', discountPercent: 0 },
      { seatId: 's2', label: 'A2', seatTypeName: 'Standard', basePrice: 100000, price: 75000, patronCategoryId: 'cat-student', patronCategoryName: 'Student', discountPercent: 25 },
    ],
    foods: [{ foodAndDrinkId: 'f1', name: 'Popcorn', unitPrice: 50000, quantity: 2 }],
  };

  const build = (historyState: unknown, queryParams: Record<string, string> = {}) => {
    Object.defineProperty(window, 'history', { value: { state: historyState }, writable: true });

    router = { navigate: vi.fn() };
    payment = {
      createBooking: vi.fn().mockReturnValue(of({ invoiceId: 'inv-1', invoiceCode: 'CIN123' })),
      validateGiftCard: vi.fn(),
      initiatePayment: vi.fn().mockReturnValue(of({ alreadyPaid: true })),
      confirmPayment: vi.fn(),
    };
    identity = { getProfile: vi.fn().mockReturnValue(of({ points: 10 })) };
    hub = { connectionId: 'my-connection', stopConnection: vi.fn() };
    cdr = { markForCheck: vi.fn() };
    const route = { snapshot: { queryParams } };
    const translate = { instant: (key: string, params?: any) => `${key}${params ? ':' + JSON.stringify(params) : ''}` };

    const c = new BookingCheckoutComponent(
      route as never, router as never, payment as never, identity as never, hub as never, cdr as never, translate as never,
    );
    c.ngOnInit();
    return c;
  };

  it('hydrates seats/foods from navigation state', () => {
    const c = build(state);

    expect(c.seats).toEqual(state.seats);
    expect(c.foods).toEqual(state.foods);
    expect(c.totalPrice).toBe(175000);
    expect(c.foodTotal).toBe(100000);
  });

  it('starts the 15-minute hold countdown on init', () => {
    const c = build(state);

    expect(c.holdSecondsLeft).toBe(15 * 60);
    expect(c.holdExpired).toBe(false);
  });

  it('redirects back to seat selection when navigation state is missing', () => {
    build(undefined, { showTimeId: 'st-1', roomId: 'room-1' });

    expect(router.navigate).toHaveBeenCalledWith(['/booking/seats'], { queryParams: { showTimeId: 'st-1', roomId: 'room-1' } });
  });

  it('redirects home when state and query params are both missing', () => {
    build(undefined, {});

    expect(router.navigate).toHaveBeenCalledWith(['/']);
  });

  it('sends one BookingSeatItem per seat, each with its own patron category', () => {
    const c = build(state);

    c.confirmBooking();

    const request = payment.createBooking.mock.calls[0][0];
    expect(request.seats).toEqual([
      { seatId: 's1', patronCategoryId: 'cat-adult' },
      { seatId: 's2', patronCategoryId: 'cat-student' },
    ]);
    expect(request.connectionId).toBe('my-connection');
  });

  it('stops the hub connection on destroy', () => {
    const c = build(state);

    c.ngOnDestroy();

    expect(hub.stopConnection).toHaveBeenCalled();
  });

  it('stops the hub connection even when navigating back to seats', () => {
    // BookingHubService.startConnection always opens a NEW connection rather than reusing one, so
    // keeping this one alive across the navigation would leak it — page 1 re-locks fine on a fresh
    // connection when the customer re-selects seats there.
    const c = build(state);

    c.backToSeats();
    c.ngOnDestroy();

    expect(hub.stopConnection).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/booking/seats'], { queryParams: { showTimeId: 'st-1', roomId: 'room-1' } });
  });
});

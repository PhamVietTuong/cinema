import { of, throwError } from 'rxjs';
import { BookingCheckoutComponent } from './booking-checkout.component';
import { BookingCheckoutState } from './booking-checkout.state';

describe('BookingCheckoutComponent', () => {
  let router: { navigate: ReturnType<typeof vi.fn>; navigateByUrl: ReturnType<typeof vi.fn> };
  let payment: { createBooking: ReturnType<typeof vi.fn>; validateGiftCard: ReturnType<typeof vi.fn>; initiatePayment: ReturnType<typeof vi.fn>; confirmPayment: ReturnType<typeof vi.fn> };
  let identity: { getProfile: ReturnType<typeof vi.fn> };
  let hub: { connectionId: string; stopConnection: ReturnType<typeof vi.fn> };
  let cdr: { markForCheck: ReturnType<typeof vi.fn> };

  const state: BookingCheckoutState = {
    showTimeId: 'st-1',
    roomId: 'room-1',
    seats: [
      { seatId: 's1', label: 'A1', seatKind: 'Standard', basePrice: 100000, price: 100000, patronCategoryId: 'cat-adult', patronCategoryName: 'Adult' },
      { seatId: 's2', label: 'A2', seatKind: 'Standard', basePrice: 100000, price: 75000, patronCategoryId: 'cat-student', patronCategoryName: 'Student' },
    ],
    foods: [{ foodAndDrinkId: 'f1', name: 'Popcorn', unitPrice: 50000, quantity: 2 }],
  };

  const build = (historyState: unknown, queryParams: Record<string, string> = {}, loyaltyRate: { pointsPerUnit: number; pointValueVnd: number; isOverride: boolean } | null = { pointsPerUnit: 1000, pointValueVnd: 1000, isOverride: false }) => {
    Object.defineProperty(window, 'history', { value: { state: historyState }, writable: true });

    router = { navigate: vi.fn(), navigateByUrl: vi.fn() };
    payment = {
      createBooking: vi.fn().mockReturnValue(of({ invoiceId: 'inv-1', invoiceCode: 'CIN123' })),
      validateGiftCard: vi.fn(),
      initiatePayment: vi.fn().mockReturnValue(of({ alreadyPaid: true })),
      confirmPayment: vi.fn(),
    };
    identity = { getProfile: vi.fn().mockReturnValue(of({ points: 10 })) };
    hub = { connectionId: 'my-connection', stopConnection: vi.fn() };
    cdr = { markForCheck: vi.fn() };
    const cinema = {
      getShowTime: vi.fn().mockReturnValue(of({ startTime: new Date(), roomName: '03', movieId: 'm1' })),
      getMovie: vi.fn().mockReturnValue(of({ title: 'Test Movie', ageRestrictionCode: 'T13' })),
      getRoom: vi.fn().mockReturnValue(of({ name: '03', theaterId: 't1' })),
      getTheater: vi.fn().mockReturnValue(of({ name: 'Cinema 1', address: 'Addr' })),
      getEffectiveLoyaltyRate: vi.fn().mockReturnValue(
        loyaltyRate ? of(loyaltyRate) : throwError(() => ({ error: 'unavailable' }))
      ),
    };
    const route = { snapshot: { queryParams } };
    const translate = { instant: (key: string, params?: any) => `${key}${params ? ':' + JSON.stringify(params) : ''}` };

    const c = new BookingCheckoutComponent(
      route as never, router as never, payment as never, identity as never, hub as never, cdr as never, translate as never, cinema as never, { open: vi.fn() } as never,
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

  it('classifies stock errors case-insensitively', () => {
    expect(BookingCheckoutComponent.isStockError("'Popcorn' is out of stock or has insufficient quantity.")).toBe(true);
    expect(BookingCheckoutComponent.isStockError('OUT OF STOCK')).toBe(true);
    expect(BookingCheckoutComponent.isStockError('Insufficient quantity')).toBe(true);
    expect(BookingCheckoutComponent.isStockError('Seat already taken')).toBe(false);
    expect(BookingCheckoutComponent.isStockError(undefined)).toBe(false);
  });

  it('flags a stock problem when createBooking fails with an out-of-stock message', () => {
    const c = build(state);
    payment.createBooking.mockReturnValue(throwError(() => ({ error: "'Popcorn' is out of stock or has insufficient quantity." })));

    c.confirmBooking();

    expect(c.error).toContain('out of stock');
    expect(c.stockProblem).toBe(true);
    expect(c.loading).toBe(false);
  });

  it('initiates payment with provider VNPay when VNPay is selected', () => {
    const c = build(state);
    c.paymentMethod = 'VNPay';

    c.confirmBooking();

    const request = payment.initiatePayment.mock.calls[0][0];
    expect(request.provider).toBe('VNPay');
  });

  it('does not flag a stock problem for other booking failures', () => {
    const c = build(state);
    payment.createBooking.mockReturnValue(throwError(() => ({ error: 'Seat is locked' })));

    c.confirmBooking();

    expect(c.stockProblem).toBe(false);
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
    expect(router.navigate).toHaveBeenCalledWith(['/booking/seats'], {
      queryParams: { showTimeId: 'st-1', roomId: 'room-1' },
      state: { restore: { showTimeId: 'st-1', roomId: 'room-1', seats: state.seats, foods: state.foods } },
    });
  });

  it('sends "back to seats" to the movie-detail page with the order to restore, when it came from there', () => {
    const c = build({ ...state, backUrl: '/movies/m1/the-movie' });

    c.backToSeats();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/movies/m1/the-movie', {
      state: { restore: { showTimeId: 'st-1', roomId: 'room-1', seats: state.seats, foods: state.foods } },
    });
  });

  it('loads the theater-specific loyalty rate and applies it to redemption math', () => {
    const c = build(state, {}, { pointsPerUnit: 1000, pointValueVnd: 2000, isOverride: true });

    expect(c.pointValue).toBe(2000);
    // grandTotal is 275000 (175000 tickets + 100000 food); balance is 10.
    expect(c.maxRedeemablePoints).toBe(10);
    c.pointsToRedeem = 5;
    expect(c.pointsDiscount).toBe(10000);
  });

  it('disables redemption (max 0) when the loyalty rate fails to load', () => {
    const c = build(state, {}, null);

    expect(c.pointValue).toBe(0);
    expect(c.maxRedeemablePoints).toBe(0);
  });

  it('projects points earned on payment from the effective earn rate', () => {
    const c = build(state, {}, { pointsPerUnit: 2000, pointValueVnd: 1000, isOverride: false });

    // finalTotal is 275000 with no redemption; 275000 / 2000 = 137.5 -> floor 137.
    expect(c.pointsToEarn).toBe(137);
  });

  it('shows zero points to earn when the earn rate fails to load', () => {
    const c = build(state, {}, null);

    expect(c.pointsToEarn).toBe(0);
  });
});

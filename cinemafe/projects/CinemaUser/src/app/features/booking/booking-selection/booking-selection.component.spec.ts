import { Subject, of } from 'rxjs';
import { BookingSelectionComponent } from './booking-selection.component';
import { PaymentServiceAgent, CinemaServiceAgent } from 'CinemaLib';

type Seat = PaymentServiceAgent.SeatDTO & { isSelected?: boolean };

const seat = (over: Partial<Seat>): Seat => ({
  id: 'seat-1',
  rowName: 'A',
  colIndex: 1,
  seatTypeId: 'standard',
  price: 100000,
  status: PaymentServiceAgent.SeatStatus.Available,
  isLocked: false,
  ...over,
} as Seat);

const category = (over: Partial<CinemaServiceAgent.PatronCategoryDTO>): CinemaServiceAgent.PatronCategoryDTO => ({
  id: 'cat-adult',
  name: 'Adult',
  discountPercent: 0,
  allowedSeatTypeIds: [],
  ...over,
} as CinemaServiceAgent.PatronCategoryDTO);

describe('BookingSelectionComponent', () => {
  let component: BookingSelectionComponent;
  let router: { navigate: ReturnType<typeof vi.fn> };
  let hub: {
    seatLocked$: Subject<{ seatId: string; connectionId: string }>;
    seatUnlocked$: Subject<string>;
    seatLockFailed$: Subject<{ seatId: string; message: string }>;
    seatBooked$: Subject<string[]>;
    connectionId: string;
    lockSeat: ReturnType<typeof vi.fn>;
    unlockSeat: ReturnType<typeof vi.fn>;
    startConnection: ReturnType<typeof vi.fn>;
    stopConnection: ReturnType<typeof vi.fn>;
  };
  let payment: { getSeats: ReturnType<typeof vi.fn> };
  let cinema: { getRoom: ReturnType<typeof vi.fn>; getFoodAndDrinks: ReturnType<typeof vi.fn>; getPatronCategories: ReturnType<typeof vi.fn> };

  /** Builds the component targeting st-1/room-1 by default, mirroring the first @Input binding
   * Angular would apply before the initial ngOnChanges/ngOnInit pass. */
  const build = (seats: Seat[], categories: CinemaServiceAgent.PatronCategoryDTO[] = [category({})]) => {
    hub = {
      seatLocked$: new Subject(),
      seatUnlocked$: new Subject(),
      seatLockFailed$: new Subject(),
      seatBooked$: new Subject(),
      connectionId: 'my-connection',
      lockSeat: vi.fn().mockResolvedValue(undefined),
      unlockSeat: vi.fn().mockResolvedValue(undefined),
      startConnection: vi.fn().mockResolvedValue(undefined),
      stopConnection: vi.fn().mockResolvedValue(undefined),
    };
    payment = { getSeats: vi.fn().mockReturnValue(of({ results: seats })) };
    cinema = {
      getRoom: vi.fn().mockReturnValue(of({ theaterId: 'theater-1' })),
      getFoodAndDrinks: vi.fn().mockReturnValue(of({ results: [] })),
      getPatronCategories: vi.fn().mockReturnValue(of({ results: categories })),
    };
    router = { navigate: vi.fn().mockResolvedValue(true) };

    const cdr = { markForCheck: vi.fn() };
    const translate = { instant: (key: string, params?: any) => `${key}${params ? ':' + JSON.stringify(params) : ''}` };

    const c = new BookingSelectionComponent(
      router as never, payment as never, cinema as never, hub as never, cdr as never, translate as never,
    );
    c.showTimeId = 'st-1';
    c.roomId = 'room-1';
    // Real Angular order: ngOnChanges (first binding) runs before ngOnInit.
    c.ngOnChanges();
    c.ngOnInit();
    return c;
  };

  describe('loading', () => {
    beforeEach(() => {
      component = build([
        seat({ id: 's1', rowName: 'A', colIndex: 1 }),
        seat({ id: 's2', rowName: 'A', colIndex: 3 }),
        seat({ id: 's3', rowName: 'B', colIndex: 2 }),
      ]);
    });

    it('adopts showTimeId/roomId from inputs', () => {
      expect(component.showTimeId).toBe('st-1');
      expect(component.roomId).toBe('room-1');
    });

    it('exposes distinct rows and clears the loading flag', () => {
      expect(component.rows).toEqual(['A', 'B']);
      expect(component.loadingSeats).toBe(false);
    });

    it('orders a row by column index', () => {
      expect(component.getSeatsByRow('A').map(s => s.id)).toEqual(['s1', 's2']);
    });

    it('starts with zero ticket quantities and an empty seat cap', () => {
      expect(component.totalTickets).toBe(0);
      expect(component.ticketQty['cat-adult']).toBe(0);
    });
  });

  describe('ticket quantities', () => {
    it('increments/decrements build and shrink the slot list', () => {
      component = build([seat({ id: 's1' })]);
      const adult = component.patronCategories[0];

      component.incTicket(adult);
      component.incTicket(adult);
      expect(component.totalTickets).toBe(2);

      component.decTicket(adult);
      expect(component.totalTickets).toBe(1);
    });

    it('caps the running total at MAX_TICKETS across categories', () => {
      component = build([seat({ id: 's1' })], [
        category({ id: 'cat-a' }),
        category({ id: 'cat-b', name: 'Student' }),
      ]);
      const [a, b] = component.patronCategories;

      component.setTicketQty(a.id!, 6);
      component.setTicketQty(b.id!, 6);

      expect(component.totalTickets).toBe(BookingSelectionComponent.MAX_TICKETS);
    });
  });

  describe('toggleSeat', () => {
    it('does nothing until at least one ticket is chosen', () => {
      component = build([seat({ id: 's1', price: 90000 })], []);

      component.toggleSeat(component.seats[0]);

      expect(component.selectedSeats).toEqual([]);
      expect(hub.lockSeat).not.toHaveBeenCalled();
    });

    it('selects a free seat and locks it on the hub', () => {
      component = build([seat({ id: 's1', price: 90000 })]);
      component.incTicket(component.patronCategories[0]);

      component.toggleSeat(component.seats[0]);

      expect(component.selectedSeats.map(s => s.id)).toEqual(['s1']);
      expect(component.totalPrice).toBe(90000);
      expect(hub.lockSeat).toHaveBeenCalledWith('st-1', 'room-1', 's1');
    });

    it('is a no-op once every ticket slot is filled', () => {
      component = build([
        seat({ id: 's1', colIndex: 1 }),
        seat({ id: 's2', colIndex: 2 }),
      ]);
      component.incTicket(component.patronCategories[0]);
      component.toggleSeat(component.seats[0]);

      component.toggleSeat(component.seats[1]);

      expect(component.selectedSeats.map(s => s.id)).toEqual(['s1']);
      expect(hub.lockSeat).toHaveBeenCalledTimes(1);
    });

    it('ignores an occupied seat', () => {
      component = build([seat({ id: 's1', status: PaymentServiceAgent.SeatStatus.Occupied })]);
      component.incTicket(component.patronCategories[0]);

      component.toggleSeat(component.seats[0]);

      expect(component.selectedSeats).toEqual([]);
      expect(hub.lockSeat).not.toHaveBeenCalled();
    });

    it('ignores a seat another viewer is holding', () => {
      component = build([seat({ id: 's1', isLocked: true })]);
      component.incTicket(component.patronCategories[0]);

      component.toggleSeat(component.seats[0]);

      expect(component.selectedSeats).toEqual([]);
      expect(hub.lockSeat).not.toHaveBeenCalled();
    });

    it('deselecting releases the lock, drops the price and frees the slot', () => {
      component = build([seat({ id: 's1', price: 90000 })]);
      component.incTicket(component.patronCategories[0]);
      component.toggleSeat(component.seats[0]);

      component.toggleSeat(component.seats[0]);

      expect(component.selectedSeats).toEqual([]);
      expect(component.totalPrice).toBe(0);
      expect(hub.unlockSeat).toHaveBeenCalledWith('st-1', 'room-1', 's1');
      expect(component.remainingTickets).toBe(1);
    });

    it('assigns the most-constrained matching category first', () => {
      component = build(
        [
          seat({ id: 'standard-seat', colIndex: 1, seatTypeId: 'standard' }),
          seat({ id: 'vip-seat', colIndex: 2, seatTypeId: 'vip' }),
        ],
        [
          category({ id: 'cat-adult', name: 'Adult', allowedSeatTypeIds: [] }),
          category({ id: 'cat-child', name: 'Child', allowedSeatTypeIds: ['standard'] }),
        ],
      );
      component.incTicket(component.patronCategories.find(c => c.id === 'cat-adult')!);
      component.incTicket(component.patronCategories.find(c => c.id === 'cat-child')!);

      component.toggleSeat(component.seats.find(s => s.id === 'standard-seat')!);
      component.toggleSeat(component.seats.find(s => s.id === 'vip-seat')!);

      expect(component.categoryForSeat('standard-seat')?.id).toBe('cat-child');
      expect(component.categoryForSeat('vip-seat')?.id).toBe('cat-adult');
    });

    it('marks a seat unavailable when no chosen category allows its type', () => {
      component = build(
        [seat({ id: 'vip-seat', seatTypeId: 'vip' })],
        [category({ id: 'cat-child', name: 'Child', allowedSeatTypeIds: ['standard'] })],
      );
      component.incTicket(component.patronCategories[0]);

      expect(component.seats[0].isAllowedForPatronCategory).toBe(false);
      component.toggleSeat(component.seats[0]);
      expect(component.selectedSeats).toEqual([]);
    });

    describe('double seats (shared seatGroupId)', () => {
      beforeEach(() => {
        component = build([
          seat({ id: 'd1', colIndex: 1, seatGroupId: 'g1', price: 80000 }),
          seat({ id: 'd2', colIndex: 2, seatGroupId: 'g1', price: 80000 }),
        ]);
      });

      it('selects and locks both halves together, consuming two slots', () => {
        component.incTicket(component.patronCategories[0]);
        component.incTicket(component.patronCategories[0]);

        component.toggleSeat(component.seats[0]);

        expect(component.selectedSeats.map(s => s.id)).toEqual(['d1', 'd2']);
        expect(component.totalPrice).toBe(160000);
        expect(hub.lockSeat).toHaveBeenCalledTimes(2);
      });

      it('refuses the pair when only one ticket remains', () => {
        component.incTicket(component.patronCategories[0]);

        component.toggleSeat(component.seats[0]);

        expect(component.selectedSeats).toEqual([]);
        expect(hub.lockSeat).not.toHaveBeenCalled();
      });

      it('refuses the pair when either half is unavailable', () => {
        component.incTicket(component.patronCategories[0]);
        component.incTicket(component.patronCategories[0]);
        component.seats[1].status = PaymentServiceAgent.SeatStatus.Occupied;

        component.toggleSeat(component.seats[0]);

        expect(component.selectedSeats).toEqual([]);
        expect(hub.lockSeat).not.toHaveBeenCalled();
      });
    });
  });

  describe('reducing a ticket quantity', () => {
    it('deselects and unlocks the surplus seat, leaving others untouched', () => {
      component = build(
        [seat({ id: 's1', colIndex: 1 }), seat({ id: 's2', colIndex: 2 })],
        [
          category({ id: 'cat-a', allowedSeatTypeIds: [] }),
          category({ id: 'cat-b', name: 'Student', allowedSeatTypeIds: [] }),
        ],
      );
      const [a, b] = component.patronCategories;
      component.incTicket(a);
      component.incTicket(b);
      component.toggleSeat(component.seats[0]);
      component.toggleSeat(component.seats[1]);
      expect(component.selectedSeats).toHaveLength(2);

      component.decTicket(b);

      expect(component.selectedSeats).toHaveLength(1);
      expect(hub.unlockSeat).toHaveBeenCalledTimes(1);
      expect(component.categoryWarning).toContain('booking.tickets.removedByQuantityChange');
    });

    it('releases a whole double seat together, never splitting the pair', () => {
      component = build([
        seat({ id: 'd1', colIndex: 1, seatGroupId: 'g1' }),
        seat({ id: 'd2', colIndex: 2, seatGroupId: 'g1' }),
      ]);
      const adult = component.patronCategories[0];
      component.incTicket(adult);
      component.incTicket(adult);
      component.toggleSeat(component.seats[0]);
      expect(component.selectedSeats.map(s => s.id)).toEqual(['d1', 'd2']);

      component.decTicket(adult);

      expect(component.selectedSeats).toEqual([]);
      expect(hub.unlockSeat).toHaveBeenCalledWith('st-1', 'room-1', 'd1');
      expect(hub.unlockSeat).toHaveBeenCalledWith('st-1', 'room-1', 'd2');
    });
  });

  describe('live lock events', () => {
    beforeEach(() => {
      component = build([seat({ id: 's1' }), seat({ id: 's2', colIndex: 2 })]);
      component.incTicket(component.patronCategories[0]);
    });

    it("another viewer's lock disables the seat and frees its slot", () => {
      component.toggleSeat(component.seats[0]);
      hub.seatLocked$.next({ seatId: 's1', connectionId: 'someone-else' });

      expect(component.seats[0].isLocked).toBe(true);
      expect(component.remainingTickets).toBe(1);
    });

    it('our own broadcast is ignored', () => {
      hub.seatLocked$.next({ seatId: 's1', connectionId: 'my-connection' });

      expect(component.seats[0].isLocked).toBeFalsy();
    });

    it('losing the lock race deselects the seat we optimistically took', () => {
      component.toggleSeat(component.seats[0]);
      expect(component.selectedSeats).toHaveLength(1);

      hub.seatLockFailed$.next({ seatId: 's1', message: 'held by another user' });

      expect(component.seats[0].isSelected).toBe(false);
      expect(component.selectedSeats).toEqual([]);
    });

    it('an unlock re-enables the seat', () => {
      hub.seatLocked$.next({ seatId: 's1', connectionId: 'someone-else' });

      hub.seatUnlocked$.next('s1');

      expect(component.seats[0].isLocked).toBe(false);
    });

    it('an event for an unknown seat is a no-op', () => {
      expect(() => hub.seatUnlocked$.next('not-in-this-room')).not.toThrow();
    });
  });

  describe('proceedToCheckout', () => {
    it('does nothing while seats are still needed', () => {
      component = build([seat({ id: 's1' }), seat({ id: 's2', colIndex: 2 })]);
      component.incTicket(component.patronCategories[0]);
      component.incTicket(component.patronCategories[0]);
      component.toggleSeat(component.seats[0]);

      component.proceedToCheckout();

      expect(router.navigate).not.toHaveBeenCalled();
    });

    it('navigates with a flat, per-seat-category state once every slot is filled', () => {
      component = build([seat({ id: 's1', price: 90000 })]);
      component.incTicket(component.patronCategories[0]);
      component.toggleSeat(component.seats[0]);

      component.proceedToCheckout();

      expect(router.navigate).toHaveBeenCalledWith(['/booking/checkout'], {
        state: {
          showTimeId: 'st-1',
          roomId: 'room-1',
          seats: [{
            seatId: 's1', label: 'A1', seatTypeName: '', basePrice: 90000, price: 90000,
            patronCategoryId: 'cat-adult', patronCategoryName: 'Adult', discountPercent: 0,
          }],
          foods: [],
        },
        queryParams: { showTimeId: 'st-1', roomId: 'room-1' },
      });
    });

    it('does not stop the hub connection afterwards, but a plain destroy does', () => {
      component = build([seat({ id: 's1' })]);
      component.incTicket(component.patronCategories[0]);
      component.toggleSeat(component.seats[0]);
      component.proceedToCheckout();

      component.ngOnDestroy();

      expect(hub.stopConnection).not.toHaveBeenCalled();
    });

    it('re-arms the disconnect safety net when navigation is rejected (e.g. a guard throws)', async () => {
      component = build([seat({ id: 's1' })]);
      router.navigate.mockRejectedValue(new Error('chunk load failed'));
      component.incTicket(component.patronCategories[0]);
      component.toggleSeat(component.seats[0]);

      component.proceedToCheckout();
      await Promise.resolve().then(() => Promise.resolve());

      component.ngOnDestroy();

      expect(hub.stopConnection).toHaveBeenCalled();
    });
  });

  describe('ngOnDestroy', () => {
    it('stops the connection when not navigating to checkout', () => {
      component = build([seat({ id: 's1' })]);

      component.ngOnDestroy();

      expect(hub.stopConnection).toHaveBeenCalled();
    });
  });

  describe('seat-hold countdown', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it('is inactive until a seat is selected', () => {
      component = build([seat({ id: 's1' })]);

      expect(component.holdActive).toBe(false);
    });

    it('starts at 5:00 when a seat is selected and ticks down', () => {
      component = build([seat({ id: 's1' })]);
      component.incTicket(component.patronCategories[0]);

      component.toggleSeat(component.seats[0]);

      expect(component.holdActive).toBe(true);
      expect(component.holdSecondsLeft).toBe(300);

      vi.advanceTimersByTime(30_000);

      expect(component.holdSecondsLeft).toBe(270);
      expect(component.holdExpired).toBe(false);
    });

    it('flips to expired once 5 minutes have elapsed', () => {
      component = build([seat({ id: 's1' })]);
      component.incTicket(component.patronCategories[0]);
      component.toggleSeat(component.seats[0]);

      vi.advanceTimersByTime(5 * 60 * 1000 + 1000);

      expect(component.holdExpired).toBe(true);
      expect(component.holdSecondsLeft).toBe(0);
    });

    it('counts down to the soonest-expiring seat when seats are locked at different times', () => {
      component = build([
        seat({ id: 's1', colIndex: 1 }),
        seat({ id: 's2', colIndex: 2 }),
      ]);
      component.incTicket(component.patronCategories[0]);
      component.incTicket(component.patronCategories[0]);
      component.toggleSeat(component.seats[0]);

      vi.advanceTimersByTime(60_000);
      component.toggleSeat(component.seats[1]);

      // s1 was locked 60s before s2, so it still expires first.
      expect(component.holdSecondsLeft).toBe(240);
    });

    it('goes inactive again once every held seat is deselected', () => {
      component = build([seat({ id: 's1' })]);
      component.incTicket(component.patronCategories[0]);
      component.toggleSeat(component.seats[0]);

      component.toggleSeat(component.seats[0]);

      expect(component.holdActive).toBe(false);
    });
  });

  describe('switching showtimes (inline-panel re-targeting)', () => {
    it('releases every selected seat of the old showtime, then stops, then starts the new connection, in order', async () => {
      component = build([seat({ id: 's1' })]);
      component.incTicket(component.patronCategories[0]);
      component.toggleSeat(component.seats[0]);
      const callOrder: string[] = [];
      hub.unlockSeat.mockImplementation((...args: unknown[]) => { callOrder.push('unlock:' + args[2]); return Promise.resolve(); });
      hub.stopConnection.mockImplementation(() => { callOrder.push('stop'); return Promise.resolve(); });
      hub.startConnection.mockImplementation(() => { callOrder.push('start'); return Promise.resolve(); });

      component.showTimeId = 'st-2';
      component.roomId = 'room-2';
      component.ngOnChanges();
      await Promise.resolve().then(() => Promise.resolve()).then(() => Promise.resolve());

      expect(callOrder).toEqual(['unlock:s1', 'stop', 'start']);
      expect(hub.unlockSeat).toHaveBeenCalledWith('st-1', 'room-1', 's1');
      expect(hub.startConnection).toHaveBeenCalledWith('st-2', 'room-2');
    });

    it('resets slots/quantities/selection/food and refetches everything for the new target', async () => {
      component = build([seat({ id: 's1' })]);
      component.incTicket(component.patronCategories[0]);
      component.toggleSeat(component.seats[0]);
      payment.getSeats.mockReturnValue(of({ results: [seat({ id: 's2', roomId: 'room-2' } as never)] }));

      component.showTimeId = 'st-2';
      component.roomId = 'room-2';
      component.ngOnChanges();
      await Promise.resolve().then(() => Promise.resolve()).then(() => Promise.resolve());

      expect(component.selectedSeats).toEqual([]);
      expect(component.slots).toEqual([]);
      expect(component.ticketQty).toEqual({ 'cat-adult': 0 });
      expect(component.holdActive).toBe(false);
      expect(payment.getSeats).toHaveBeenCalledTimes(2);
      expect(cinema.getRoom).toHaveBeenCalledWith('room-2');
      expect(cinema.getPatronCategories).toHaveBeenCalledTimes(2);
      expect(cinema.getFoodAndDrinks).toHaveBeenCalledTimes(2);
    });

    it('discards a stale response for a showtime switched away from', async () => {
      // Built by hand (not the `build()` helper) so the FIRST getSeats call never resolves
      // synchronously — it must still be "in flight" when we switch away from st-1/room-1.
      let capturedObserver: { next: (v: unknown) => void } | undefined;
      let callCount = 0;
      hub = {
        seatLocked$: new Subject(), seatUnlocked$: new Subject(), seatLockFailed$: new Subject(), seatBooked$: new Subject(),
        connectionId: 'my-connection',
        lockSeat: vi.fn().mockResolvedValue(undefined), unlockSeat: vi.fn().mockResolvedValue(undefined),
        startConnection: vi.fn().mockResolvedValue(undefined), stopConnection: vi.fn().mockResolvedValue(undefined),
      };
      payment = {
        getSeats: vi.fn().mockImplementation(() => {
          callCount++;
          if (callCount === 1) {
            return { subscribe: (observer: { next: (v: unknown) => void }) => { capturedObserver = observer; } };
          }
          return of({ results: [seat({ id: 'fresh-seat' })] });
        }),
      };
      cinema = {
        getRoom: vi.fn().mockReturnValue(of({ theaterId: 'theater-1' })),
        getFoodAndDrinks: vi.fn().mockReturnValue(of({ results: [] })),
        getPatronCategories: vi.fn().mockReturnValue(of({ results: [category({})] })),
      };
      router = { navigate: vi.fn().mockResolvedValue(true) };
      const cdr = { markForCheck: vi.fn() };
      const translate = { instant: (key: string) => key };
      const c = new BookingSelectionComponent(router as never, payment as never, cinema as never, hub as never, cdr as never, translate as never);
      c.showTimeId = 'st-1';
      c.roomId = 'room-1';
      c.ngOnChanges(); // kicks off the slow initial load for st-1/room-1; seats still []
      c.ngOnInit();

      c.showTimeId = 'st-2';
      c.roomId = 'room-2';
      c.ngOnChanges(); // queues the switch to st-2/room-2
      await Promise.resolve().then(() => Promise.resolve()).then(() => Promise.resolve());

      // Only now does the stale st-1/room-1 response (in flight since before the switch) arrive.
      capturedObserver?.next({ results: [seat({ id: 'stale-seat' })] });

      expect(c.seats.some(s => s.id === 'stale-seat')).toBe(false);
      expect(c.seats.some(s => s.id === 'fresh-seat')).toBe(true);
    });

    it('wires hub-event subscriptions exactly once across two switches', async () => {
      component = build([seat({ id: 's1' })]);
      component.incTicket(component.patronCategories[0]);
      component.toggleSeat(component.seats[0]);

      component.showTimeId = 'st-2';
      component.roomId = 'room-2';
      component.ngOnChanges();
      await Promise.resolve().then(() => Promise.resolve()).then(() => Promise.resolve());
      component.incTicket(component.patronCategories[0]);
      component.toggleSeat(component.seats[0]);

      component.showTimeId = 'st-3';
      component.roomId = 'room-3';
      component.ngOnChanges();
      await Promise.resolve().then(() => Promise.resolve()).then(() => Promise.resolve());

      hub.seatBooked$.next(['s1']);
      // A single subscriber means the seat is marked booked once, not applied N times over.
      expect(component.selectedSeats.filter(s => s.id === 's1')).toHaveLength(0);
    });
  });

  describe('cancel', () => {
    it('unlocks selected seats, stops the connection, resets state, and emits closed', async () => {
      component = build([seat({ id: 's1' })]);
      component.incTicket(component.patronCategories[0]);
      component.toggleSeat(component.seats[0]);
      const closedSpy = vi.fn();
      component.closed.subscribe(closedSpy);

      component.cancel();
      await Promise.resolve().then(() => Promise.resolve()).then(() => Promise.resolve());

      expect(hub.unlockSeat).toHaveBeenCalledWith('st-1', 'room-1', 's1');
      expect(hub.stopConnection).toHaveBeenCalled();
      expect(component.selectedSeats).toEqual([]);
      expect(component.patronCategories).toEqual([]);
      expect(closedSpy).toHaveBeenCalled();
    });
  });
});

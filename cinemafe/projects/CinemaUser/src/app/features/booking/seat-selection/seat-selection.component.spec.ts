import { Subject, of } from 'rxjs';
import { SeatSelectionComponent } from './seat-selection.component';
import { PaymentServiceAgent } from 'CinemaLib';

type Seat = PaymentServiceAgent.SeatDTO & { isSelected?: boolean };

const seat = (over: Partial<Seat>): Seat => ({
  id: 'seat-1',
  rowName: 'A',
  colIndex: 1,
  price: 100000,
  status: PaymentServiceAgent.SeatStatus.Available,
  isLocked: false,
  ...over,
} as Seat);

describe('SeatSelectionComponent', () => {
  let component: SeatSelectionComponent;
  let hub: {
    seatLocked$: Subject<{ seatId: string; connectionId: string }>;
    seatUnlocked$: Subject<string>;
    seatLockFailed$: Subject<{ seatId: string; message: string }>;
    connectionId: string;
    lockSeat: ReturnType<typeof vi.fn>;
    unlockSeat: ReturnType<typeof vi.fn>;
    startConnection: ReturnType<typeof vi.fn>;
    stopConnection: ReturnType<typeof vi.fn>;
  };
  let router: { navigate: ReturnType<typeof vi.fn> };
  let payment: { getSeats: ReturnType<typeof vi.fn> };

  const build = (seats: Seat[]) => {
    hub = {
      seatLocked$: new Subject(),
      seatUnlocked$: new Subject(),
      seatLockFailed$: new Subject(),
      connectionId: 'my-connection',
      lockSeat: vi.fn().mockResolvedValue(undefined),
      unlockSeat: vi.fn().mockResolvedValue(undefined),
      startConnection: vi.fn().mockResolvedValue(undefined),
      stopConnection: vi.fn(),
    };
    router = { navigate: vi.fn() };
    payment = { getSeats: vi.fn().mockReturnValue(of({ results: seats })) };

    const route = { snapshot: { queryParams: { showTimeId: 'st-1', roomId: 'room-1' } } };
    const cdr = { markForCheck: vi.fn() };

    const c = new SeatSelectionComponent(
      route as never, router as never, payment as never, hub as never, cdr as never,
    );
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

    it('reads showTimeId and roomId off the query params', () => {
      expect(component.showTimeId).toBe('st-1');
      expect(component.roomId).toBe('room-1');
    });

    it('exposes distinct rows and clears the loading flag', () => {
      expect(component.rows).toEqual(['A', 'B']);
      expect(component.loading).toBe(false);
    });

    it('orders a row by column index', () => {
      expect(component.getSeatsByRow('A').map(s => s.id)).toEqual(['s1', 's2']);
    });
  });

  describe('toggleSeat', () => {
    it('selects a free seat, locks it on the hub and adds its price', () => {
      component = build([seat({ id: 's1', price: 90000 })]);

      component.toggleSeat(component.seats[0]);

      expect(component.selectedSeats.map(s => s.id)).toEqual(['s1']);
      expect(component.totalPrice).toBe(90000);
      expect(hub.lockSeat).toHaveBeenCalledWith('st-1', 'room-1', 's1');
    });

    it('deselecting releases the lock and drops the price', () => {
      component = build([seat({ id: 's1', price: 90000 })]);

      component.toggleSeat(component.seats[0]);
      component.toggleSeat(component.seats[0]);

      expect(component.selectedSeats).toEqual([]);
      expect(component.totalPrice).toBe(0);
      expect(hub.unlockSeat).toHaveBeenCalledWith('st-1', 'room-1', 's1');
    });

    it('ignores an occupied seat', () => {
      component = build([seat({ id: 's1', status: PaymentServiceAgent.SeatStatus.Occupied })]);

      component.toggleSeat(component.seats[0]);

      expect(component.selectedSeats).toEqual([]);
      expect(hub.lockSeat).not.toHaveBeenCalled();
    });

    it('ignores a seat another viewer is holding', () => {
      component = build([seat({ id: 's1', isLocked: true })]);

      component.toggleSeat(component.seats[0]);

      expect(component.selectedSeats).toEqual([]);
      expect(hub.lockSeat).not.toHaveBeenCalled();
    });

    describe('double seats (shared seatGroupId)', () => {
      beforeEach(() => {
        component = build([
          seat({ id: 'd1', colIndex: 1, seatGroupId: 'g1', price: 80000 }),
          seat({ id: 'd2', colIndex: 2, seatGroupId: 'g1', price: 80000 }),
        ]);
      });

      it('selects and locks both halves together', () => {
        component.toggleSeat(component.seats[0]);

        expect(component.selectedSeats.map(s => s.id)).toEqual(['d1', 'd2']);
        expect(component.totalPrice).toBe(160000);
        expect(hub.lockSeat).toHaveBeenCalledTimes(2);
      });

      it('refuses the pair when either half is unavailable', () => {
        component.seats[1].status = PaymentServiceAgent.SeatStatus.Occupied;

        component.toggleSeat(component.seats[0]);

        expect(component.selectedSeats).toEqual([]);
        expect(hub.lockSeat).not.toHaveBeenCalled();
      });
    });
  });

  describe('live lock events', () => {
    beforeEach(() => {
      component = build([seat({ id: 's1' }), seat({ id: 's2', colIndex: 2 })]);
    });

    it("another viewer's lock disables the seat", () => {
      hub.seatLocked$.next({ seatId: 's1', connectionId: 'someone-else' });

      expect(component.seats[0].isLocked).toBe(true);
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
    it('carries the showtime, room and selection in navigation state', () => {
      component = build([seat({ id: 's1' })]);
      component.toggleSeat(component.seats[0]);

      component.proceedToCheckout();

      expect(router.navigate).toHaveBeenCalledWith(['/booking/confirmation'], {
        state: { showTimeId: 'st-1', roomId: 'room-1', seats: component.selectedSeats },
      });
    });
  });

  describe('ngOnDestroy', () => {
    it('stops the connection but leaves held seats locked for checkout', () => {
      component = build([seat({ id: 's1' })]);
      component.toggleSeat(component.seats[0]);
      hub.unlockSeat.mockClear();

      component.ngOnDestroy();

      expect(hub.stopConnection).toHaveBeenCalled();
      expect(hub.unlockSeat).not.toHaveBeenCalled();
    });

    it('unsubscribes from hub events', () => {
      component = build([seat({ id: 's1' })]);

      component.ngOnDestroy();
      hub.seatLocked$.next({ seatId: 's1', connectionId: 'someone-else' });

      expect(component.seats[0].isLocked).toBeFalsy();
    });
  });
});

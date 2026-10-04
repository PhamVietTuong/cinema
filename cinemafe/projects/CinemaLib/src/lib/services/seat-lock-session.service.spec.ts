import { Subject } from 'rxjs';
import { SeatLockSessionService } from './seat-lock-session.service';

const makeHub = () => ({
  seatLocked$: new Subject<{ seatId: string; connectionId: string }>(),
  seatUnlocked$: new Subject<string>(),
  seatLockFailed$: new Subject<{ seatId: string; message: string }>(),
  seatBooked$: new Subject<string[]>(),
  connectionId: 'mine',
  lockSeat: vi.fn().mockResolvedValue(undefined),
  unlockSeat: vi.fn().mockResolvedValue(undefined),
  startConnection: vi.fn().mockResolvedValue(undefined),
  stopConnection: vi.fn().mockResolvedValue(undefined),
});

describe('SeatLockSessionService', () => {
  let hub: ReturnType<typeof makeHub>;
  let session: SeatLockSessionService;

  beforeEach(() => {
    hub = makeHub();
    session = new SeatLockSessionService(hub as never);
  });

  it('starts the hub for the showtime and room and swallows a failed handshake', async () => {
    hub.startConnection.mockRejectedValueOnce(new Error('boom'));
    await expect(session.start('st', 'room')).resolves.toBeUndefined();
    expect(hub.startConnection).toHaveBeenCalledWith('st', 'room');
  });

  it('locks and unlocks on the current target', async () => {
    await session.start('st', 'room');
    session.lock('s1');
    session.unlock('s1');
    expect(hub.lockSeat).toHaveBeenCalledWith('st', 'room', 's1');
    expect(hub.unlockSeat).toHaveBeenCalledWith('st', 'room', 's1');
  });

  it('republishes other connections locks but drops its own', () => {
    const seen: string[] = [];
    session.locked$.subscribe(id => seen.push(id));
    hub.seatLocked$.next({ seatId: 'x', connectionId: 'mine' });
    hub.seatLocked$.next({ seatId: 'y', connectionId: 'other' });
    expect(seen).toEqual(['y']);
  });

  it('republishes unlock, failure and booked events', () => {
    const out: string[] = [];
    session.unlocked$.subscribe(id => out.push(`u:${id}`));
    session.lockFailed$.subscribe(id => out.push(`f:${id}`));
    session.booked$.subscribe(ids => out.push(`b:${ids.join(',')}`));
    hub.seatUnlocked$.next('a');
    hub.seatLockFailed$.next({ seatId: 'b', message: '' });
    hub.seatBooked$.next(['c', 'd']);
    expect(out).toEqual(['u:a', 'f:b', 'b:c,d']);
  });

  it('computes the soonest hold expiry among held seats', () => {
    vi.useFakeTimers();
    vi.setSystemTime(1_000_000);
    session.lock('a');
    vi.setSystemTime(1_060_000);
    session.lock('b');
    expect(session.soonestExpiry(['a', 'b'])).toBe(1_000_000 + SeatLockSessionService.LOCK_HOLD_MS);
    expect(session.soonestExpiry(['b'])).toBe(1_060_000 + SeatLockSessionService.LOCK_HOLD_MS);
    expect(session.soonestExpiry(['zzz'])).toBeNull();
    session.forget('a');
    expect(session.soonestExpiry(['a'])).toBeNull();
    vi.useRealTimers();
  });

  it('stops the hub connection', async () => {
    await session.stop();
    expect(hub.stopConnection).toHaveBeenCalled();
  });
});

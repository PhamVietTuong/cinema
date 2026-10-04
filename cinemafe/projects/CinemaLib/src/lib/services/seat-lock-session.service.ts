import { Injectable, OnDestroy } from '@angular/core';
import { Subject, Subscription } from 'rxjs';
import { BookingHubService } from './booking-hub.service';

/**
 * One screen's seat-lock session on the booking hub: it targets a showtime + room, tracks when each of
 * its own seats was locked (for the 5-minute hold countdown) and republishes the hub's room events with
 * its own broadcasts already filtered out. Used by the customer booking page and the counter POS.
 *
 * Not root-provided: each screen creates its own (`providers: [SeatLockSessionService]` on the component,
 * or `new SeatLockSessionService(hub)`), so the session lives and dies with the screen. The shared
 * BookingHubService underneath is still the single SignalR connection.
 */
@Injectable()
export class SeatLockSessionService implements OnDestroy {
  /** Mirrors BookingManager.IsSeatLocked's 5-minute expiry; keep in sync with the backend. */
  static readonly LOCK_HOLD_MS = 5 * 60 * 1000;

  /** A seat was locked by ANOTHER connection. */
  readonly locked$ = new Subject<string>();
  /** A seat was released (by whoever held it). */
  readonly unlocked$ = new Subject<string>();
  /** Our own lock attempt lost the race: the seat is held by someone else. */
  readonly lockFailed$ = new Subject<string>();
  /** Seats were sold; they are permanently gone. */
  readonly booked$ = new Subject<string[]>();

  private _showTimeId = '';
  private _roomId = '';
  private readonly _lockedAt = new Map<string, number>();
  private readonly _subs = new Subscription();

  constructor(private readonly _hub: BookingHubService) {
    this._subs.add(this._hub.seatLocked$.subscribe(e => {
      if (e.connectionId === this._hub.connectionId) {
        return; // our own lock: ignore
      }
      this.locked$.next(e.seatId);
    }));
    this._subs.add(this._hub.seatUnlocked$.subscribe(seatId => this.unlocked$.next(seatId)));
    this._subs.add(this._hub.seatLockFailed$.subscribe(e => this.lockFailed$.next(e.seatId)));
    this._subs.add(this._hub.seatBooked$.subscribe(seatIds => this.booked$.next(seatIds)));
  }

  /** ConnectionId of the live hub connection (the key the API checks seat holds against), or null. */
  get connectionId(): string | null {
    return this._hub.connectionId;
  }

  /**
   * Targets a showtime + room and opens the hub connection for it. A failed handshake is swallowed so the
   * screen degrades to non-realtime instead of breaking.
   */
  start(showTimeId: string, roomId: string): Promise<void> {
    this._showTimeId = showTimeId;
    this._roomId = roomId;
    return this._hub.startConnection(showTimeId, roomId).catch(() => { /* degrade to non-realtime */ });
  }

  /** Closes the connection, which releases every seat lock it holds on the server. */
  async stop(): Promise<void> {
    this._lockedAt.clear();
    await this._hub.stopConnection();
  }

  /** Locks a seat on the current target and starts its hold clock. */
  lock(seatId: string): void {
    this._lockedAt.set(seatId, Date.now());
    this._hub.lockSeat(this._showTimeId, this._roomId, seatId).catch(() => { /* see lockFailed$ */ });
  }

  /** Releases a seat on the current target. */
  unlock(seatId: string): void {
    this._lockedAt.delete(seatId);
    this._hub.unlockSeat(this._showTimeId, this._roomId, seatId).catch(() => {});
  }

  /** Stops tracking a seat's hold clock without telling the hub (its lock is already gone). */
  forget(seatId: string): void {
    this._lockedAt.delete(seatId);
  }

  /** Forgets every hold clock (the target changed or the selection was reset). */
  forgetAll(): void {
    this._lockedAt.clear();
  }

  /**
   * Soonest lock-expiry timestamp (ms epoch) among the given seats, or null when none of them is held.
   * That seat's lock lapses first, so it is the one worth counting down to.
   */
  soonestExpiry(seatIds: readonly string[]): number | null {
    const stamps = seatIds
      .map(id => this._lockedAt.get(id))
      .filter((t): t is number => t !== undefined);
    if (stamps.length === 0) {
      return null;
    }
    return Math.min(...stamps) + SeatLockSessionService.LOCK_HOLD_MS;
  }

  /** Stops listening to the hub. Called by DI when the owning component is destroyed, or by hand. */
  ngOnDestroy(): void {
    this._subs.unsubscribe();
  }
}

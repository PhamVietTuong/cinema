import { Injectable, inject, OnDestroy } from '@angular/core';
import type { HubConnection } from '@microsoft/signalr';
import { BehaviorSubject, Subject } from 'rxjs';
import { HUB_BASE_URL } from '../tokens';
import { Store } from '@ngrx/store';
import { selectToken } from '../store/auth/auth.selectors';
import { take } from 'rxjs/operators';

export interface SeatLockEvent {
  seatId: string;
}

export interface SeatLockFailedEvent {
  seatId: string;
  message: string;
}

@Injectable({ providedIn: 'root' })
export class BookingHubService implements OnDestroy {
  private connection: HubConnection | null = null;
  private store = inject(Store);
  private hubUrl = inject(HUB_BASE_URL);

  seatLocked$ = new Subject<SeatLockEvent>();
  seatUnlocked$ = new Subject<string>();
  seatLockFailed$ = new Subject<SeatLockFailedEvent>();
  seatBooked$ = new Subject<string[]>();
  connected$ = new BehaviorSubject<boolean>(false);

  /** ConnectionId of the live hub connection (sent with a booking so the API can match it to our seat holds). */
  get connectionId(): string | null {
    return this.connection?.connectionId ?? null;
  }

  async startConnection(showTimeId: string, roomId: string): Promise<void> {
    // Never leave a previous connection running (its handlers and group membership would leak).
    if (this.connection) {
      await this.stopConnection();
    }
    // SignalR is loaded on first use (opening seat selection), not in the initial bundle.
    const { HubConnectionBuilder, LogLevel } = await import('@microsoft/signalr');
    let token = '';
    this.store.select(selectToken).pipe(take(1)).subscribe(t => token = t ?? '');

    const connection = new HubConnectionBuilder()
      .withUrl(`${this.hubUrl}/booking`, { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    // The server broadcasts SeatLocked to everyone EXCEPT the locker and never includes the locker's connection id.
    connection.on('SeatLocked', (seatId: string) =>
      this.seatLocked$.next({ seatId }));

    connection.on('SeatUnlocked', (seatId: string) =>
      this.seatUnlocked$.next(seatId));

    connection.on('SeatLockFailed', (seatId: string, message: string) =>
      this.seatLockFailed$.next({ seatId, message }));

    connection.on('SeatBooked', (seatIds: string[]) =>
      this.seatBooked$.next(seatIds));

    // Re-join the room group after an automatic reconnect (a reconnect uses a new
    // connection that is no longer a member of the group).
    connection.onreconnected(() => { connection.invoke('JoinRoom', showTimeId, roomId); });

    this.connection = connection;
    await connection.start();
    this.connected$.next(true);
    // Join the room group so we receive other viewers' lock events before our first lock.
    await connection.invoke('JoinRoom', showTimeId, roomId);
  }

  async lockSeat(showTimeId: string, roomId: string, seatId: string): Promise<void> {
    await this.connection?.invoke('LockSeat', showTimeId, roomId, seatId);
  }

  async unlockSeat(showTimeId: string, roomId: string, seatId: string): Promise<void> {
    await this.connection?.invoke('UnlockSeat', showTimeId, roomId, seatId);
  }

  async stopConnection(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    await connection?.stop();
    this.connected$.next(false);
  }

  ngOnDestroy(): void {
    this.stopConnection();
  }
}

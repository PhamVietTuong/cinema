import { Injectable, inject, OnDestroy } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
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
    let token = '';
    this.store.select(selectToken).pipe(take(1)).subscribe(t => token = t ?? '');

    this.connection = new HubConnectionBuilder()
      .withUrl(`${this.hubUrl}/booking`, { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    // The server broadcasts SeatLocked to everyone EXCEPT the locker and never includes the locker's connection id.
    this.connection.on('SeatLocked', (seatId: string) =>
      this.seatLocked$.next({ seatId }));

    this.connection.on('SeatUnlocked', (seatId: string) =>
      this.seatUnlocked$.next(seatId));

    this.connection.on('SeatLockFailed', (seatId: string, message: string) =>
      this.seatLockFailed$.next({ seatId, message }));

    this.connection.on('SeatBooked', (seatIds: string[]) =>
      this.seatBooked$.next(seatIds));

    // Re-join the room group after an automatic reconnect (a reconnect uses a new
    // connection that is no longer a member of the group).
    this.connection.onreconnected(() => { this.connection?.invoke('JoinRoom', showTimeId, roomId); });

    await this.connection.start();
    this.connected$.next(true);
    // Join the room group so we receive other viewers' lock events before our first lock.
    await this.connection.invoke('JoinRoom', showTimeId, roomId);
  }

  async lockSeat(showTimeId: string, roomId: string, seatId: string): Promise<void> {
    await this.connection?.invoke('LockSeat', showTimeId, roomId, seatId);
  }

  async unlockSeat(showTimeId: string, roomId: string, seatId: string): Promise<void> {
    await this.connection?.invoke('UnlockSeat', showTimeId, roomId, seatId);
  }

  async stopConnection(): Promise<void> {
    await this.connection?.stop();
    this.connected$.next(false);
  }

  ngOnDestroy(): void {
    this.stopConnection();
  }
}

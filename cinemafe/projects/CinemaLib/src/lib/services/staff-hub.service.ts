import { Injectable, OnDestroy, inject } from '@angular/core';
import type { HubConnection } from '@microsoft/signalr';
import { Store } from '@ngrx/store';
import { BehaviorSubject, Subject, firstValueFrom } from 'rxjs';
import { selectToken } from '../store/auth/auth.selectors';
import { HUB_BASE_URL } from '../tokens';
import { CinemaServiceAgent } from './cinema-http.service';

/**
 * Live connection to the staff hub (`/hubs/staff`). The server joins the theater groups listed in the
 * user's claims on connect; an Admin picks a theater with `setTheater`. The connection is shared by every
 * staff screen: `start()` is idempotent and nothing stops it until the service is destroyed.
 */
@Injectable({ providedIn: 'root' })
export class StaffHubService implements OnDestroy {
  private readonly _store = inject(Store);
  private readonly _hubUrl = inject(HUB_BASE_URL);

  private _connection: HubConnection | null = null;
  private _starting: Promise<void> | null = null;
  private _theaterId: string | null = null;

  readonly incidentRaised$ = new Subject<CinemaServiceAgent.IncidentDTO>();
  readonly connected$ = new BehaviorSubject<boolean>(false);
  /** Emits after an automatic reconnect: events may have been missed, so screens refetch. */
  readonly reconnected$ = new Subject<void>();

  /** Connects once; later calls wait for the same connection. */
  start(): Promise<void> {
    if (this._isConnected()) {
      return Promise.resolve();
    }
    if (!this._starting) {
      this._starting = this._connect().finally(() => {
        this._starting = null;
      });
    }
    return this._starting;
  }

  /** Admin only: follows one theater's group (null leaves the current one). Re-applied after a reconnect. */
  async setTheater(theaterId: string | null): Promise<void> {
    const previous = this._theaterId;
    if (previous === theaterId) {
      return;
    }
    this._theaterId = theaterId;
    const connection = this._connection;
    if (!connection || !this._isConnected()) {
      return;
    }
    if (previous) {
      await connection.invoke('LeaveTheater', previous);
    }
    if (theaterId) {
      await connection.invoke('JoinTheater', theaterId);
    }
  }

  /** HubConnectionState.Connected, compared by value so the SignalR runtime isn't imported eagerly. */
  private _isConnected(): boolean {
    return (this._connection?.state as string | undefined) === 'Connected';
  }

  async stop(): Promise<void> {
    const connection = this._connection;
    this._connection = null;
    await connection?.stop();
    this.connected$.next(false);
  }

  ngOnDestroy(): void {
    void this.stop();
  }

  private async _connect(): Promise<void> {
    // SignalR is loaded on first use (a staff screen opening the hub), not in the initial bundle.
    const { HubConnectionBuilder, LogLevel } = await import('@microsoft/signalr');
    const connection = new HubConnectionBuilder()
      .withUrl(`${this._hubUrl}/staff`, {
        // Read at every (re)connect so a refreshed token is picked up.
        accessTokenFactory: async () => (await firstValueFrom(this._store.select(selectToken))) ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('IncidentRaised', (incident: unknown) =>
      this.incidentRaised$.next(CinemaServiceAgent.IncidentDTO.fromJS(incident)));

    connection.onreconnecting(() => this.connected$.next(false));
    connection.onreconnected(async () => {
      this.connected$.next(true);
      // A reconnect is a new connection that is no longer in the theater group.
      if (this._theaterId) {
        try {
          await connection.invoke('JoinTheater', this._theaterId);
        } catch {
          /* the screens still refetch below; a failed join only costs live updates */
        }
      }
      this.reconnected$.next();
    });
    connection.onclose(() => this.connected$.next(false));

    this._connection = connection;
    await connection.start();
    this.connected$.next(true);
    if (this._theaterId) {
      await connection.invoke('JoinTheater', this._theaterId);
    }
  }
}

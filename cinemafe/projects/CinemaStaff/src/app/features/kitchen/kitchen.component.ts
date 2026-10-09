import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import {
  EmptyStateComponent,
  SharedModule,
  StaffHubService,
  CinemaServiceAgent,
  StatusPillComponent,
  showException,
} from 'CinemaLib';
import { StaffLiveService } from '../../core/staff-live.service';
import { TheaterContextService } from '../../core/theater-context.service';
import {
  QUEUE_COLUMNS,
  applyQueued,
  applyUpdated,
  minutesSince,
  needsRefetch,
  nextFoodStatus,
  ordersInColumn,
} from './kitchen.state';

type Order = CinemaServiceAgent.PickupOrderDTO;

/** i18n key of the action button that advances an order out of the given status. */
const ACTION_KEYS: Partial<Record<CinemaServiceAgent.FoodOrderStatus, string>> = {
  [CinemaServiceAgent.FoodOrderStatus.Pending]: 'kitchen.action.start',
  [CinemaServiceAgent.FoodOrderStatus.Preparing]: 'kitchen.action.ready',
  [CinemaServiceAgent.FoodOrderStatus.Ready]: 'kitchen.action.handOver',
};

/** i18n key of each board column heading. */
const COLUMN_KEYS: Partial<Record<CinemaServiceAgent.FoodOrderStatus, string>> = {
  [CinemaServiceAgent.FoodOrderStatus.Pending]: 'kitchen.status.pending',
  [CinemaServiceAgent.FoodOrderStatus.Preparing]: 'kitchen.status.preparing',
  [CinemaServiceAgent.FoodOrderStatus.Ready]: 'kitchen.status.ready',
};

/**
 * Food pickup board: Pending / Preparing / Ready columns kept live by the staff hub, a lookup box for
 * a pickup code and the low-stock panel. Handing an order over removes its card.
 */
@Component({
  selector: 'staff-kitchen',
  standalone: true,
  imports: [SharedModule, StatusPillComponent, EmptyStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './kitchen.component.html',
  styleUrl: './kitchen.component.scss',
})
export class KitchenComponent {
  readonly hub = inject(StaffHubService);
  readonly live = inject(StaffLiveService);
  private readonly _concession = inject(CinemaServiceAgent.HttpService);
  private readonly _theater = inject(TheaterContextService);
  private readonly _store = inject(Store);
  private readonly _fb = inject(FormBuilder);
  private readonly _destroyRef = inject(DestroyRef);

  readonly columns = QUEUE_COLUMNS;
  readonly theaterId = this._theater.currentTheaterId;
  readonly queue = signal<Order[]>([]);
  readonly busy = signal<ReadonlySet<string>>(new Set());
  readonly found = signal<Order | null>(null);
  readonly lookupMissing = signal(false);
  /** Ticks every 30 s so "time since paid" stays current. */
  private readonly _now = signal(new Date());

  readonly lookupForm = this._fb.group({
    code: ['', [Validators.required, Validators.maxLength(50)]],
  });

  constructor() {
    const timer = setInterval(() => this._now.set(new Date()), 30000);
    this._destroyRef.onDestroy(() => clearInterval(timer));

    // Reload whenever the theater changes (an Admin switching theaters in the topbar).
    effect(() => {
      if (this.theaterId()) {
        this.load();
      } else {
        this.queue.set([]);
      }
    });

    this.hub.foodOrderQueued$.pipe(takeUntilDestroyed()).subscribe(order => {
      if (order.theaterId === this.theaterId()) {
        this.queue.update(queue => applyQueued(queue, order));
      }
    });
    this.hub.foodOrderUpdated$.pipe(takeUntilDestroyed()).subscribe(event => {
      if (event.theaterId !== this.theaterId()) {
        return;
      }
      if (needsRefetch(this.queue(), event)) {
        this.load();
        return;
      }
      this.queue.update(queue => applyUpdated(queue, event));
    });
    // Events may have been missed while disconnected.
    this.hub.reconnected$.pipe(takeUntilDestroyed()).subscribe(() => this.load());
  }

  ordersOf(status: CinemaServiceAgent.FoodOrderStatus): Order[] {
    return ordersInColumn(this.queue(), status);
  }

  columnKey(status: CinemaServiceAgent.FoodOrderStatus): string {
    return COLUMN_KEYS[status] ?? '';
  }

  actionKey(order: Order): string | null {
    return ACTION_KEYS[order.foodStatus as CinemaServiceAgent.FoodOrderStatus] ?? null;
  }

  minutesAgo(order: Order): number {
    return minutesSince(order.paidAt, this._now());
  }

  load(): void {
    const theaterId = this.theaterId();
    if (!theaterId) {
      return;
    }
    this._concession.getPickupQueue(CinemaServiceAgent.GetPickupQueueRequest.fromJS({ theaterId })).subscribe({
      next: orders => this.queue.set(orders ?? []),
      error: error => this._store.dispatch(showException({ error })),
    });
  }

  advance(order: Order): void {
    const next = nextFoodStatus(order.foodStatus);
    const invoiceId = order.invoiceId;
    if (next === null || !invoiceId) {
      return;
    }
    this._setBusy(invoiceId, true);
    this._concession.setFoodStatus(CinemaServiceAgent.SetFoodStatusRequest.fromJS({
      theaterId: this.theaterId() ?? undefined,
      invoiceId,
      status: next,
    })).subscribe({
      next: () => {
        // The hub also announces this change; applying it here keeps the board right if the hub is down.
        this.queue.update(queue => applyUpdated(queue, { invoiceId, foodStatus: next }));
        const lookedUp = this.found();
        if (lookedUp?.invoiceId === invoiceId) {
          this.found.set(this._withStatus(lookedUp, next));
        }
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._setBusy(invoiceId, false));
  }

  lookup(): void {
    const code = (this.lookupForm.value.code ?? '').trim();
    if (!code) {
      return;
    }
    this.lookupMissing.set(false);
    this._concession.lookupPickup(CinemaServiceAgent.LookupPickupRequest.fromJS({
      theaterId: this.theaterId() ?? undefined,
      code,
    })).subscribe({
      next: order => this.found.set(order),
      error: error => {
        this.found.set(null);
        if (error?.status === 404) {
          this.lookupMissing.set(true);
        } else {
          this._store.dispatch(showException({ error }));
        }
      },
    });
  }

  private _withStatus(order: Order, status: CinemaServiceAgent.FoodOrderStatus): Order {
    const copy = CinemaServiceAgent.PickupOrderDTO.fromJS(order.toJSON());
    copy.foodStatus = status;
    return copy;
  }

  private _setBusy(invoiceId: string, busy: boolean): void {
    this.busy.update(current => {
      const next = new Set(current);
      if (busy) {
        next.add(invoiceId);
      } else {
        next.delete(invoiceId);
      }
      return next;
    });
  }
}

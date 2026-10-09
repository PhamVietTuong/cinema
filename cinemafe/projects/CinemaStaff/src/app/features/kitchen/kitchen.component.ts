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
  template: `
<div class="ad-page">
  <div class="ad-page-header">
    <div>
      <h1 class="ad-h1">{{ 'kitchen.title' | translate }}</h1>
      <p class="ad-sub">{{ 'kitchen.subtitle' | translate }}</p>
    </div>
    <div class="ad-toolbar">
      <span class="ad-pill" [class.ad-pill--success]="hub.connected$ | async" [class.ad-pill--danger]="!(hub.connected$ | async)">
        {{ ((hub.connected$ | async) ? 'kitchen.live' : 'kitchen.offline') | translate }}
      </span>
      <button mat-stroked-button type="button" (click)="load()" [disabled]="!theaterId()">
        <mat-icon>refresh</mat-icon> {{ 'kitchen.refresh' | translate }}
      </button>
    </div>
  </div>

  @if (!theaterId()) {
    <mat-card class="ad-card--pad-0">
      <cl-empty-state icon="theaters" messageKey="kitchen.pickTheater" hintKey="kitchen.pickTheaterHint" />
    </mat-card>
  } @else {
    <div class="top-row">
      <form class="ad-card lookup" [formGroup]="lookupForm" (ngSubmit)="lookup()">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ 'kitchen.lookup.code' | translate }}</mat-label>
          <input matInput formControlName="code" autocomplete="off">
        </mat-form-field>
        <button mat-raised-button color="primary" type="submit" [disabled]="lookupForm.invalid">
          <mat-icon>search</mat-icon> {{ 'kitchen.lookup.search' | translate }}
        </button>
      </form>

      <section class="ad-card stock" [class.stock--alert]="live.lowStockCount() > 0">
        <h3 class="ad-card-title">
          <mat-icon>warning</mat-icon> {{ 'stockAlerts.title' | translate }}
          @if (live.lowStockCount() > 0) {
            <span class="ad-pill ad-pill--warn">{{ live.lowStockCount() }}</span>
          }
        </h3>
        @for (item of live.lowStock(); track item.foodAndDrinkId) {
          <div class="stock-row">
            <strong>{{ item.name }}</strong>
            <span>{{ 'stockAlerts.onHand' | translate: { qty: item.quantityOnHand, threshold: item.lowStockThreshold } }}</span>
          </div>
        } @empty {
          <p class="muted">{{ 'stockAlerts.none' | translate }}</p>
        }
      </section>
    </div>

    @if (found(); as order) {
      <div class="found">
        <h3 class="ad-card-title">{{ 'kitchen.lookup.result' | translate }}</h3>
        <ng-container *ngTemplateOutlet="card; context: { $implicit: order }" />
      </div>
    } @else if (lookupMissing()) {
      <p class="muted">{{ 'kitchen.lookup.notFound' | translate }}</p>
    }

    <div class="board">
      @for (column of columns; track column) {
        <section class="column">
          <h3 class="column-head">
            {{ columnKey(column) | translate }}
            <span class="count">{{ ordersOf(column).length }}</span>
          </h3>
          @for (order of ordersOf(column); track order.invoiceId) {
            <ng-container *ngTemplateOutlet="card; context: { $implicit: order }" />
          } @empty {
            <cl-empty-state icon="restaurant" messageKey="kitchen.emptyColumn" />
          }
        </section>
      }
    </div>
  }
</div>

<ng-template #card let-order>
  <mat-card class="order">
    <div class="order-head">
      <strong>{{ order.invoiceCode }}</strong>
      <cl-status-pill kind="foodOrder" [value]="order.foodStatus" />
    </div>
    <div class="muted">
      {{ order.movieTitle }} &middot; {{ order.roomName }} &middot; {{ order.showTimeStart | date: 'HH:mm dd/MM' }}
    </div>
    <ul class="items">
      @for (item of order.items ?? []; track $index) {
        <li><strong>{{ item.quantity }}&times;</strong> {{ item.name }}</li>
      }
    </ul>
    <div class="order-foot">
      <span class="muted">
        {{ order.customerName }}
        @if (order.paidAt) {
          &middot; {{ 'kitchen.paidAgo' | translate: { minutes: minutesAgo(order) } }}
        }
      </span>
      @if (actionKey(order); as key) {
        <button mat-flat-button color="primary" type="button" [disabled]="busy().has(order.invoiceId)" (click)="advance(order)">
          {{ key | translate }}
        </button>
      }
    </div>
  </mat-card>
</ng-template>
`,
  styles: [`
    .top-row { display: grid; grid-template-columns: minmax(280px, 1fr) minmax(280px, 1fr); gap: 16px; margin-bottom: 16px; align-items: start; }
    .lookup { display: flex; gap: 12px; align-items: center; padding: 16px; margin: 0; }
    .lookup mat-form-field { flex: 1; }
    .stock { padding: 16px; margin: 0; }
    .stock--alert { border-left: 4px solid var(--ml-warn-ink, #b26a00); }
    .stock .ad-card-title { display: flex; align-items: center; gap: 8px; }
    .stock-row { display: flex; justify-content: space-between; gap: 12px; padding: 4px 0; }
    .muted { color: var(--ml-muted); }
    .found { margin-bottom: 16px; max-width: 480px; }
    .board { display: grid; grid-template-columns: repeat(3, minmax(240px, 1fr)); gap: 16px; align-items: start; }
    .column { background: var(--ml-panel-2, rgba(0, 0, 0, 0.03)); border-radius: 12px; padding: 12px; min-height: 160px; }
    .column-head { display: flex; align-items: center; justify-content: space-between; margin: 0 0 12px; font-size: 14px; text-transform: uppercase; letter-spacing: 0.04em; }
    .count { font-variant-numeric: tabular-nums; color: var(--ml-muted); }
    .order { padding: 12px 16px; margin-bottom: 12px; }
    .order-head { display: flex; justify-content: space-between; align-items: center; gap: 8px; margin-bottom: 4px; }
    .items { margin: 8px 0; padding-left: 18px; }
    .order-foot { display: flex; justify-content: space-between; align-items: center; gap: 8px; flex-wrap: wrap; }
    @media (max-width: 900px) {
      .top-row, .board { grid-template-columns: 1fr; }
    }
  `],
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

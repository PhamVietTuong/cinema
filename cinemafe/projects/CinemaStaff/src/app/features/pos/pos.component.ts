import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { EmptyStateComponent, SeatLockSessionService, SharedModule, CinemaServiceAgent, ToastService, apiErrorMessage } from 'CinemaLib';
import { CashDrawerService } from '../../core/cash-drawer.service';
import { CounterCartComponent } from '../../core/counter-cart.component';
import { CounterCartService } from '../../core/counter-cart.service';
import { CounterSummaryComponent } from '../../core/counter-summary.component';
import { CounterTenderPanelComponent } from '../../core/counter-tender-panel.component';
import { TenderEntry, isCash, settle } from '../../core/pos-calc';
import { PosReceiptComponent, SaleReceipt } from '../../core/pos-receipt.component';
import { TheaterContextService } from '../../core/theater-context.service';

/**
 * Counter point of sale: pick today's showtime (or sell food only), pick seats and a patron category per seat,
 * add food and combos, optionally attach a member, then take cash / card / QR tenders and print the tickets.
 * The cart (showtime, seats, food, member, quote) lives in `CounterCartService`; the server's Quote drives every
 * total shown and selling goes through BoxOffice.Sell.
 */
@Component({
  selector: 'staff-pos',
  standalone: true,
  imports: [SharedModule, CounterCartComponent, CounterSummaryComponent, CounterTenderPanelComponent, PosReceiptComponent, EmptyStateComponent],
  providers: [SeatLockSessionService, CounterCartService],
  templateUrl: './pos.component.html',
  styleUrl: './pos.component.scss',
})
export class PosComponent {
  private readonly _box = inject(CinemaServiceAgent.HttpService);
  private readonly _translate = inject(TranslateService);
  private readonly _toast = inject(ToastService);
  readonly cart = inject(CounterCartService);
  readonly theaterCtx = inject(TheaterContextService);
  readonly drawer = inject(CashDrawerService);

  readonly theaterId = this.cart.theaterId;

  readonly tenders = signal<TenderEntry[]>([]);
  readonly selling = signal(false);
  readonly receipt = signal<SaleReceipt | null>(null);

  readonly settlement = computed(() => settle(this.cart.due(), this.tenders()));
  readonly needsDrawer = computed(() => this.settlement().usesCash && !this.drawer.isOpen());
  readonly canSell = computed(() =>
    !!this.cart.quote() && !this.cart.quoting() && !this.selling() && this.cart.hasItems() && this.settlement().complete && !this.needsDrawer());

  constructor() {
    // The cart reloads itself for the theater; the counter-only parts follow.
    effect(() => {
      const id = this.theaterId();
      untracked(() => {
        this.tenders.set([]);
        this.receipt.set(null);
        if (id) {
          this.drawer.refresh().subscribe({ error: () => {} });
        }
      });
    });
  }

  sell(): void {
    const req = this.cart.quoteRequest();
    if (!req || !this.canSell()) {
      return;
    }
    const st = this.cart.showtime();
    const tickets = this.cart.tickets();
    const full = {
      ...req,
      connectionId: this.cart.connectionId,
      tenders: this.tenders().map(t => ({
        method: t.method,
        amount: Math.round(t.amount),
        reference: isCash(t.method) ? undefined : t.reference.trim(),
      })),
    };
    this.selling.set(true);
    this._box.sell(CinemaServiceAgent.CounterSaleRequest.fromJS(full)).subscribe({
      next: result => {
        this.selling.set(false);
        this.receipt.set({
          result,
          movieTitle: tickets.length > 0 ? (st?.movieTitle ?? '') : '',
          roomName: tickets.length > 0 ? (st?.roomName ?? '') : '',
          showTime: tickets.length > 0 ? st?.startTime : undefined,
          theaterName: this.theaterCtx.currentTheaterName() ?? '',
        });
        this._toast.success(this._translate.instant('pos.sell.success', { code: result.invoiceCode }));
        this.tenders.set([]);
        this.cart.afterSale(tickets);
        this.drawer.refresh().subscribe({ error: () => {} });
      },
      error: err => {
        this.selling.set(false);
        this._toast.error(apiErrorMessage(err, this._translate.instant('pos.sell.failed')));
      },
    });
  }

  newSale(): void {
    this.receipt.set(null);
  }
}

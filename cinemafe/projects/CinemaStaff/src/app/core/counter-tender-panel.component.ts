import { Component, computed, inject, input, model } from '@angular/core';
import { CounterTenderValues, SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { CashDrawerService } from './cash-drawer.service';
import { TenderEntry, isCash, remainingAfter, settle } from './pos-calc';

/**
 * Cash / card / QR tender lines against an amount due, with the settlement summary and its warnings.
 * Shared by the POS (whole total) and the exchange dialog (price difference only). The host owns the `tenders`
 * model and the submit button, projected as content under the summary.
 */
@Component({
  selector: 'staff-counter-tender-panel',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './counter-tender-panel.component.html',
  styleUrl: './counter-tender-panel.component.scss',
})
export class CounterTenderPanelComponent {
  private readonly _drawer = inject(CashDrawerService);

  readonly tenderOptions = CounterTenderValues;
  readonly isCash = isCash;

  /** Amount the tenders must cover (whole dong). */
  readonly due = input.required<number>();
  readonly titleKey = input('pos.pay.title');
  readonly dueLabelKey = input('pos.pay.due');
  /** Blocks adding tender lines (nothing quoted yet). */
  readonly disabled = input(false);
  readonly tenders = model<TenderEntry[]>([]);

  readonly settlement = computed(() => settle(this.due(), this.tenders()));
  readonly needsDrawer = computed(() => this.settlement().usesCash && !this._drawer.isOpen());

  add(method: CinemaServiceAgent.PaymentTender): void {
    const entries = this.tenders();
    this.tenders.set([...entries, { method, amount: remainingAfter(this.due(), entries, -1), reference: '' }]);
  }

  update(index: number, patch: Partial<TenderEntry>): void {
    this.tenders.update(list => list.map((t, i) => (i === index ? { ...t, ...patch } : t)));
  }

  fillRemaining(index: number): void {
    this.update(index, { amount: remainingAfter(this.due(), this.tenders(), index) });
  }

  remove(index: number): void {
    this.tenders.update(list => list.filter((_, i) => i !== index));
  }
}

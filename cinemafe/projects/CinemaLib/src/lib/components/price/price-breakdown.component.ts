import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';

/** One charged line (a ticket, a combo, a snack). */
export interface PriceBreakdownLine {
  /** Display text (already translated or a product name). */
  label: string;
  /** Extra muted text under the label, e.g. the ticket category. */
  detail?: string;
  quantity?: number;
  /** The line total. */
  amount: number;
  /** Original unit price shown struck through when the line was repriced (override / member price). */
  listAmount?: number;
}

/** A deduction (discount, redeemed points, gift card). `amount` is the positive value taken off. */
export interface PriceBreakdownAdjustment {
  /** i18n key of the label. */
  labelKey: string;
  amount: number;
}

/**
 * Itemised price summary: charged lines, deductions and the amount due.
 * Usage: `<cl-price-breakdown [lines]="lines" [adjustments]="adjustments" [total]="quote.finalAmount" />`
 */
@Component({
  selector: 'cl-price-breakdown',
  standalone: true,
  imports: [DecimalPipe, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cl-pb">
      @for (line of lines(); track $index) {
        <div class="cl-pb__row">
          <span class="cl-pb__label">
            @if (line.quantity && line.quantity > 1) {
              <span class="cl-pb__qty">{{ line.quantity }} ×</span>
            }
            {{ line.label }}
            @if (line.detail) {
              <small class="cl-pb__detail">{{ line.detail }}</small>
            }
          </span>
          <span class="cl-pb__amount">
            @if (line.listAmount !== undefined && line.listAmount !== line.amount) {
              <s class="cl-pb__list">{{ line.listAmount | number:'1.0-0' }}</s>
            }
            {{ line.amount | number:'1.0-0' }}{{ currency() }}
          </span>
        </div>
      }
      @if (lines().length === 0) {
        <div class="cl-pb__empty">{{ (emptyKey()) | translate }}</div>
      }
      @if (subtotalVisible()) {
        <div class="cl-pb__row cl-pb__row--sub">
          <span>{{ 'priceBreakdown.subtotal' | translate }}</span>
          <span class="cl-pb__amount">{{ subtotal() | number:'1.0-0' }}{{ currency() }}</span>
        </div>
      }
      @for (adj of adjustments(); track adj.labelKey) {
        @if (adj.amount > 0) {
          <div class="cl-pb__row cl-pb__row--adj">
            <span>{{ adj.labelKey | translate }}</span>
            <span class="cl-pb__amount">−{{ adj.amount | number:'1.0-0' }}{{ currency() }}</span>
          </div>
        }
      }
      <div class="cl-pb__row cl-pb__row--total">
        <span>{{ totalLabelKey() | translate }}</span>
        <span class="cl-pb__amount">{{ total() | number:'1.0-0' }}{{ currency() }}</span>
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; }
    .cl-pb { display: flex; flex-direction: column; gap: 6px; font-size: 0.9rem; }
    .cl-pb__row { display: flex; justify-content: space-between; gap: 12px; align-items: baseline; }
    .cl-pb__label { display: flex; gap: 6px; align-items: baseline; flex-wrap: wrap; }
    .cl-pb__qty { color: var(--ml-muted, #777); font-variant-numeric: tabular-nums; }
    .cl-pb__detail { color: var(--ml-muted, #777); }
    .cl-pb__amount { font-variant-numeric: tabular-nums; white-space: nowrap; }
    .cl-pb__list { color: var(--ml-muted, #777); margin-right: 6px; }
    .cl-pb__empty { color: var(--ml-muted, #777); font-style: italic; }
    .cl-pb__row--sub { border-top: 1px dashed var(--ml-rule, #d8d2c4); padding-top: 6px; color: var(--ml-muted, #777); }
    .cl-pb__row--adj { color: var(--ml-success, #2e7d32); }
    .cl-pb__row--total {
      border-top: 1px solid var(--ml-rule-strong, #bbb); padding-top: 8px; margin-top: 2px;
      font-size: 1.1rem; font-weight: 700;
    }
  `],
})
export class PriceBreakdownComponent {
  readonly lines = input<readonly PriceBreakdownLine[]>([]);
  readonly adjustments = input<readonly PriceBreakdownAdjustment[]>([]);
  /** The amount due after every deduction. */
  readonly total = input(0);
  readonly totalLabelKey = input('priceBreakdown.total');
  /** i18n key shown when there are no lines. */
  readonly emptyKey = input('priceBreakdown.empty');
  readonly currency = input('đ');

  protected readonly subtotal = computed(() => this.lines().reduce((sum, l) => sum + l.amount, 0));
  /** The subtotal row only helps when deductions make it differ from the total. */
  protected readonly subtotalVisible = computed(() => this.adjustments().some(a => a.amount > 0));
}

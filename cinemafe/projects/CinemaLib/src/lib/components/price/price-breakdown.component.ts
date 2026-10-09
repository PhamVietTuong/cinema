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
  templateUrl: './price-breakdown.component.html',
  styleUrl: './price-breakdown.component.scss',
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

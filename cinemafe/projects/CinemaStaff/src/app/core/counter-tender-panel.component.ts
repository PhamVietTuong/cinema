import { Component, computed, inject, input, model } from '@angular/core';
import { CounterTenderValues, SharedModule, StaffServiceAgent } from 'CinemaLib';
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
  template: `
    <section class="ad-card">
      <h2 class="ad-card-title">{{ titleKey() | translate }}</h2>
      @for (t of tenders(); track $index; let i = $index) {
        <div class="pos-tender">
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="pos-tender-method">
            <mat-select [value]="t.method" (selectionChange)="update(i, { method: $event.value })">
              @for (o of tenderOptions; track o.value) {
                <mat-option [value]="o.value">{{ o.name | translate }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="pos-tender-amount">
            <mat-label>{{ (isCash(t.method) ? 'pos.pay.tendered' : 'pos.pay.amount') | translate }}</mat-label>
            <input matInput type="number" min="0" step="1000" [ngModel]="t.amount" (ngModelChange)="update(i, { amount: $event ?? 0 })">
          </mat-form-field>
          @if (!isCash(t.method)) {
            <mat-form-field appearance="outline" subscriptSizing="dynamic" class="pos-tender-ref">
              <mat-label>{{ 'pos.pay.reference' | translate }}</mat-label>
              <input matInput [ngModel]="t.reference" (ngModelChange)="update(i, { reference: $event ?? '' })">
            </mat-form-field>
          }
          <button mat-icon-button type="button" (click)="fillRemaining(i)" [matTooltip]="'pos.pay.fill' | translate"><mat-icon>vertical_align_bottom</mat-icon></button>
          <button mat-icon-button type="button" (click)="remove(i)" [matTooltip]="'common.remove' | translate"><mat-icon>close</mat-icon></button>
        </div>
      }
      <div class="pos-tender-add">
        @for (o of tenderOptions; track o.value) {
          <button mat-stroked-button type="button" (click)="add(o.value)" [disabled]="disabled()">+ {{ o.name | translate }}</button>
        }
      </div>

      <dl class="pos-settle">
        <div><dt>{{ dueLabelKey() | translate }}</dt><dd>{{ settlement().due | number:'1.0-0' }}đ</dd></div>
        <div><dt>{{ 'pos.pay.tenderedTotal' | translate }}</dt><dd>{{ settlement().tendered | number:'1.0-0' }}đ</dd></div>
        @if (settlement().remaining > 0) {
          <div class="is-warn"><dt>{{ 'pos.pay.remaining' | translate }}</dt><dd>{{ settlement().remaining | number:'1.0-0' }}đ</dd></div>
        }
        @if (settlement().changeDue > 0) {
          <div class="is-change"><dt>{{ 'pos.pay.change' | translate }}</dt><dd>{{ settlement().changeDue | number:'1.0-0' }}đ</dd></div>
        }
      </dl>

      @if (settlement().nonCashExcess) {
        <p class="pos-warn">{{ 'pos.pay.nonCashExcess' | translate }}</p>
      }
      @if (settlement().missingReference) {
        <p class="pos-warn">{{ 'pos.pay.missingReference' | translate }}</p>
      }
      @if (needsDrawer()) {
        <p class="pos-warn">
          {{ 'pos.pay.needDrawer' | translate }}
          <a routerLink="/drawer">{{ 'pos.drawer.openLink' | translate }}</a>
        </p>
      }
      <ng-content />
    </section>
  `,
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

  add(method: StaffServiceAgent.PaymentTender): void {
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

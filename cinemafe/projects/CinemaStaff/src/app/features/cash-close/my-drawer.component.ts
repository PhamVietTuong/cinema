import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { FormControl } from '@angular/forms';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import {
  DialogService,
  EmptyStateComponent,
  SharedModule,
  StaffServiceAgent,
  StatusPillComponent,
  hideLoading,
  showException,
  showLoading,
  showSuccess,
} from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';
import { drawerVariance, isValidCount, needsReconciliation, varianceTone } from './cash-close.logic';

/** The signed-in seller's open cash drawer: expected cash, count input, variance preview and Close drawer. */
@Component({
  selector: 'staff-my-drawer',
  standalone: true,
  imports: [SharedModule, StatusPillComponent, EmptyStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
@if (!theaterId()) {
  <mat-card class="ad-card--pad-0">
    <cl-empty-state icon="theaters" messageKey="cashClose.pickTheater" hintKey="cashClose.pickTheaterHint" />
  </mat-card>
} @else if (result(); as closed) {
  <mat-card class="panel">
    <h2 class="panel-title">{{ 'cashClose.drawer.closedTitle' | translate }}</h2>
    <dl class="summary">
      <dt>{{ 'cashClose.drawer.status' | translate }}</dt><dd><cl-status-pill kind="drawerStatus" [value]="closed.status" /></dd>
      <dt>{{ 'cashClose.drawer.expected' | translate }}</dt><dd>{{ closed.expectedCash | currency: 'VND':'symbol':'1.0-0' }}</dd>
      <dt>{{ 'cashClose.drawer.counted' | translate }}</dt><dd>{{ closed.countedCash | currency: 'VND':'symbol':'1.0-0' }}</dd>
      <dt>{{ 'cashClose.drawer.variance' | translate }}</dt>
      <dd [class]="'variance variance--' + toneOf(closed.variance)">{{ closed.variance | currency: 'VND':'symbol':'1.0-0' }}</dd>
    </dl>
    @if (closed.needsReconciliation) {
      <p class="notice warn"><mat-icon>warning</mat-icon> {{ 'cashClose.drawer.needsReconciliation' | translate }}</p>
    }
    <button mat-stroked-button type="button" (click)="result.set(null)">{{ 'common.close' | translate }}</button>
  </mat-card>
} @else if (drawer(); as d) {
  @if (!d.isOpen) {
    <mat-card class="ad-card--pad-0">
      <cl-empty-state icon="point_of_sale" messageKey="cashClose.drawer.noOpenDrawer" hintKey="cashClose.drawer.noOpenDrawerHint" />
    </mat-card>
  } @else {
    <mat-card class="panel">
      <h2 class="panel-title">{{ 'cashClose.drawer.title' | translate: { terminal: d.terminalName } }}</h2>
      <dl class="summary">
        <dt>{{ 'cashClose.drawer.openedAt' | translate }}</dt><dd>{{ d.openedAt | serverUtc | date: 'HH:mm dd/MM/yyyy' }}</dd>
        <dt>{{ 'cashClose.drawer.openingFloat' | translate }}</dt><dd>{{ d.openingFloat | currency: 'VND':'symbol':'1.0-0' }}</dd>
        <dt>{{ 'cashClose.drawer.cashSales' | translate }}</dt><dd>{{ d.cashSales | currency: 'VND':'symbol':'1.0-0' }}</dd>
        <dt>{{ 'cashClose.drawer.payIns' | translate }}</dt><dd>{{ d.payIns | currency: 'VND':'symbol':'1.0-0' }}</dd>
        <dt>{{ 'cashClose.drawer.payOuts' | translate }}</dt><dd>{{ d.payOuts | currency: 'VND':'symbol':'1.0-0' }}</dd>
        <dt>{{ 'cashClose.drawer.refunds' | translate }}</dt><dd>{{ d.refunds | currency: 'VND':'symbol':'1.0-0' }}</dd>
        <dt><strong>{{ 'cashClose.drawer.expected' | translate }}</strong></dt>
        <dd><strong>{{ d.expectedCash | currency: 'VND':'symbol':'1.0-0' }}</strong></dd>
      </dl>

      <div class="count-fields">
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{ 'cashClose.drawer.counted' | translate }}</mat-label>
          <input matInput type="number" min="0" step="1000" inputmode="numeric" [formControl]="countControl">
          @if (countControl.value !== null && !validCount()) {
            <mat-error>{{ 'cashClose.drawer.countInvalid' | translate }}</mat-error>
          }
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="note">
          <mat-label>{{ 'cashClose.drawer.note' | translate }}</mat-label>
          <input matInput maxlength="500" [formControl]="noteControl" autocomplete="off">
        </mat-form-field>
      </div>

      @if (variance() !== null) {
        <p [class]="'variance variance--' + tone()">
          {{ 'cashClose.drawer.variancePreview' | translate }}: {{ variance() | currency: 'VND':'symbol':'1.0-0' }}
        </p>
        @if (previewReconcile()) {
          <p class="notice warn"><mat-icon>warning</mat-icon> {{ 'cashClose.drawer.reconcileHint' | translate }}</p>
        }
      }

      <button mat-raised-button color="primary" type="button" [disabled]="busy() || !validCount()" (click)="close(d)">
        <mat-icon>lock</mat-icon> {{ 'cashClose.drawer.close' | translate }}
      </button>
    </mat-card>
  }
}
`,
  styles: [`
    .panel { padding: 16px 24px; max-width: 640px; }
    .panel-title { margin: 0 0 12px; font-size: 16px; }
    .summary { display: grid; grid-template-columns: 1fr auto; gap: 6px 24px; margin: 0 0 16px; }
    .summary dt { color: var(--ml-muted); }
    .summary dd { margin: 0; text-align: right; }
    .count-fields { display: flex; gap: 12px; flex-wrap: wrap; margin-bottom: 12px; }
    .note { flex: 1 1 240px; }
    .notice { display: flex; align-items: center; gap: 8px; color: var(--ml-muted); }
    .notice.warn { color: var(--ml-warn-ink); }
    .variance { font-weight: 600; }
    .variance--short { color: var(--ml-danger-ink); }
    .variance--over { color: var(--ml-warn-ink); }
    .variance--balanced { color: var(--ml-success-ink); }
  `],
})
export class MyDrawerComponent {
  private readonly _boxOffice = inject(StaffServiceAgent.BoxOfficeHttpService);
  private readonly _theater = inject(TheaterContextService);
  private readonly _store = inject(Store);
  private readonly _dialogs = inject(DialogService);
  private readonly _translate = inject(TranslateService);

  readonly theaterId = this._theater.currentTheaterId;
  readonly countControl = new FormControl<number | null>(null);
  readonly noteControl = new FormControl('');

  readonly drawer = signal<StaffServiceAgent.CashDrawerDTO | null>(null);
  readonly result = signal<StaffServiceAgent.CloseDrawerResultDTO | null>(null);
  readonly busy = signal(false);

  private readonly _count = signal<number | null>(null);
  readonly validCount = computed(() => isValidCount(this._count()));
  readonly variance = computed(() => this.validCount() ? drawerVariance(this.drawer()?.expectedCash, this._count()) : null);
  readonly tone = computed(() => varianceTone(this.variance()));
  readonly previewReconcile = computed(() => needsReconciliation(this.variance()));

  constructor() {
    this.countControl.valueChanges.subscribe(value => this._count.set(value === null || value === undefined ? null : Number(value)));
    // Reload when an Admin switches the topbar theater.
    effect(() => {
      if (this.theaterId()) {
        this.load();
      } else {
        this.drawer.set(null);
      }
    });
  }

  toneOf(variance: number | undefined): string {
    return varianceTone(variance);
  }

  load(): void {
    this._store.dispatch(showLoading());
    this._boxOffice.getMyDrawer(StaffServiceAgent.BoxOfficeScopeRequest.fromJS({ theaterId: this.theaterId() ?? undefined })).subscribe({
      next: drawer => this.drawer.set(drawer),
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }

  close(drawer: StaffServiceAgent.CashDrawerDTO): void {
    const counted = this._count();
    if (!isValidCount(counted)) {
      return;
    }
    this._dialogs.openConfirmDialog({
      title: 'cashClose.drawer.confirmTitle',
      message: 'cashClose.drawer.confirmMessage',
    }).afterClosed().subscribe(confirmed => {
      if (!confirmed) {
        return;
      }
      this.busy.set(true);
      this._store.dispatch(showLoading());
      this._boxOffice.closeDrawer(StaffServiceAgent.CloseDrawerRequest.fromJS({
        theaterId: this.theaterId() ?? undefined,
        sessionId: drawer.id,
        countedCash: counted,
        note: (this.noteControl.value ?? '').trim() || undefined,
      })).subscribe({
        next: closed => {
          this.result.set(closed);
          this._store.dispatch(showSuccess({ message: this._translate.instant('cashClose.drawer.closedToast') }));
          this.countControl.reset(null);
          this.noteControl.reset('');
          this.load();
        },
        error: error => this._store.dispatch(showException({ error })),
      }).add(() => {
        this.busy.set(false);
        this._store.dispatch(hideLoading());
      });
    });
  }
}

import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormBuilder, FormGroupDirective, Validators } from '@angular/forms';
import { TranslateService } from '@ngx-translate/core';
import {
  EmptyStateComponent, SharedModule, StaffServiceAgent, ToastService, apiErrorMessage, cashMovementTypeLabel,
} from 'CinemaLib';
import { CashDrawerService } from '../../core/cash-drawer.service';
import { ManagerApprovalService } from '../../core/manager-approval.service';
import { TheaterContextService } from '../../core/theater-context.service';

/** localStorage key remembering the terminal name the cashier last opened a drawer with. */
const TERMINAL_STORAGE_KEY = 'cinema_staff_terminal';

/** Cash drawer: open it with a float, see the running totals and movements, record pay-ins and pay-outs. */
@Component({
  selector: 'staff-drawer',
  standalone: true,
  imports: [SharedModule, EmptyStateComponent],
  template: `
    <div class="ad-page">
      <div class="ad-page-header">
        <div>
          <h1 class="ad-h1">{{ 'drawer.title' | translate }}</h1>
          <p class="ad-sub">{{ 'drawer.subtitle' | translate }}</p>
        </div>
      </div>

      @if (!theaterCtx.currentTheaterId()) {
        <mat-card class="ad-card--pad-0">
          <cl-empty-state icon="theaters" messageKey="warehouse.pickTheater" hintKey="warehouse.pickTheaterHint" />
        </mat-card>
      } @else if (!loaded()) {
        <mat-spinner diameter="36" />
      } @else if (!drawerSvc.isOpen()) {
        <section class="ad-card drawer-card">
          <h2 class="ad-card-title">{{ 'drawer.open.title' | translate }}</h2>
          <p class="muted">{{ 'drawer.open.hint' | translate }}</p>
          <form [formGroup]="openForm" (ngSubmit)="open()" class="drawer-form">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ 'drawer.open.terminal' | translate }}</mat-label>
              <input matInput formControlName="terminalName" maxlength="40">
              @if (openForm.controls.terminalName.hasError('required')) {
                <mat-error>{{ 'common.required' | translate }}</mat-error>
              }
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ 'drawer.open.float' | translate }}</mat-label>
              <input matInput type="number" min="0" step="10000" formControlName="openingFloat">
              @if (openForm.controls.openingFloat.invalid) {
                <mat-error>{{ 'drawer.amountInvalid' | translate }}</mat-error>
              }
            </mat-form-field>
            <button mat-raised-button color="primary" type="submit" [disabled]="busy()">{{ 'drawer.open.action' | translate }}</button>
          </form>
        </section>
      } @else {
        @let d = drawerSvc.drawer()!;
        <section class="ad-card drawer-card">
          <h2 class="ad-card-title">{{ 'drawer.status.title' | translate: { terminal: d.terminalName } }}</h2>
          <p class="muted">{{ 'drawer.status.openedAt' | translate }} {{ d.openedAt | serverUtc | date:'dd/MM/yyyy HH:mm' }}</p>
          <dl class="drawer-summary">
            <div><dt>{{ 'drawer.status.openingFloat' | translate }}</dt><dd>{{ d.openingFloat | number:'1.0-0' }}đ</dd></div>
            <div><dt>{{ 'drawer.status.cashSales' | translate }}</dt><dd>{{ d.cashSales | number:'1.0-0' }}đ</dd></div>
            <div><dt>{{ 'drawer.status.payIns' | translate }}</dt><dd>{{ d.payIns | number:'1.0-0' }}đ</dd></div>
            <div><dt>{{ 'drawer.status.payOuts' | translate }}</dt><dd>−{{ d.payOuts | number:'1.0-0' }}đ</dd></div>
            <div><dt>{{ 'drawer.status.refunds' | translate }}</dt><dd>−{{ d.refunds | number:'1.0-0' }}đ</dd></div>
            <div class="is-total"><dt>{{ 'drawer.status.expected' | translate }}</dt><dd>{{ d.expectedCash | number:'1.0-0' }}đ</dd></div>
          </dl>
        </section>

        <section class="ad-card drawer-card">
          <h2 class="ad-card-title">{{ 'drawer.move.title' | translate }}</h2>
          <form [formGroup]="moveForm" #moveDirective="ngForm" (ngSubmit)="move(moveDirective)" class="drawer-form">
            <mat-button-toggle-group formControlName="type">
              <mat-button-toggle [value]="CashMovementType.PayIn">{{ 'drawer.movement.payIn' | translate }}</mat-button-toggle>
              <mat-button-toggle [value]="CashMovementType.PayOut">{{ 'drawer.movement.payOut' | translate }}</mat-button-toggle>
            </mat-button-toggle-group>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ 'drawer.move.amount' | translate }}</mat-label>
              <input matInput type="number" min="1" step="10000" formControlName="amount">
              @if (moveForm.controls.amount.invalid) {
                <mat-error>{{ 'drawer.amountInvalid' | translate }}</mat-error>
              }
            </mat-form-field>
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>{{ 'drawer.move.note' | translate }}</mat-label>
              <input matInput formControlName="note" maxlength="200">
              @if (moveForm.controls.note.hasError('required')) {
                <mat-error>{{ 'common.required' | translate }}</mat-error>
              }
            </mat-form-field>
            @if (moveForm.controls.type.value === CashMovementType.PayOut) {
              <p class="muted">{{ 'drawer.move.payOutApproval' | translate }}</p>
            }
            <button mat-raised-button color="primary" type="submit" [disabled]="busy()">{{ 'drawer.move.record' | translate }}</button>
          </form>
        </section>

        <section class="ad-card drawer-card">
          <h2 class="ad-card-title">{{ 'drawer.movements.title' | translate }}</h2>
          @if ((d.recentMovements ?? []).length === 0) {
            <p class="muted">{{ 'drawer.movements.empty' | translate }}</p>
          } @else {
            <table class="drawer-movements">
              <thead>
                <tr>
                  <th>{{ 'drawer.movements.time' | translate }}</th>
                  <th>{{ 'drawer.movements.type' | translate }}</th>
                  <th class="num">{{ 'drawer.movements.amount' | translate }}</th>
                  <th>{{ 'drawer.movements.note' | translate }}</th>
                </tr>
              </thead>
              <tbody>
                @for (m of d.recentMovements; track m.id) {
                  <tr>
                    <td>{{ m.creationTime | serverUtc | date:'HH:mm' }}</td>
                    <td>{{ typeLabel(m.type) | translate }}</td>
                    <td class="num">{{ m.amount | number:'1.0-0' }}đ</td>
                    <td>{{ m.note }}</td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </section>
      }
    </div>
  `,
  styles: [`
    .drawer-card { max-width: 640px; margin-bottom: 16px; }
    .muted { color: var(--ml-muted); font-size: 0.85rem; }
    .drawer-form { display: flex; flex-direction: column; gap: 12px; }
    .drawer-summary { margin: 0; display: grid; gap: 6px; }
    .drawer-summary div { display: flex; justify-content: space-between; }
    .drawer-summary dt { color: var(--ml-muted); }
    .drawer-summary dd { margin: 0; font-variant-numeric: tabular-nums; font-weight: 600; }
    .drawer-summary .is-total { border-top: 1px solid var(--ml-rule-strong); padding-top: 8px; font-size: 1.1rem; }
    .drawer-movements { width: 100%; border-collapse: collapse; }
    .drawer-movements th, .drawer-movements td { padding: 6px 8px; text-align: left; border-bottom: 1px solid var(--ml-rule); }
    .drawer-movements .num { text-align: right; font-variant-numeric: tabular-nums; }
  `],
})
export class DrawerComponent {
  readonly CashMovementType = StaffServiceAgent.CashMovementType;
  readonly typeLabel = cashMovementTypeLabel;
  readonly theaterCtx = inject(TheaterContextService);
  readonly drawerSvc = inject(CashDrawerService);
  private readonly _approval = inject(ManagerApprovalService);
  private readonly _translate = inject(TranslateService);
  private readonly _toast = inject(ToastService);
  private readonly _fb = inject(FormBuilder);

  readonly loaded = signal(false);
  readonly busy = signal(false);
  readonly isOpen = computed(() => this.drawerSvc.isOpen());

  readonly openForm = this._fb.nonNullable.group({
    terminalName: [this._readTerminal(), Validators.required],
    openingFloat: [0, [Validators.required, Validators.min(0)]],
  });

  readonly moveForm = this._fb.nonNullable.group({
    type: [StaffServiceAgent.CashMovementType.PayIn as StaffServiceAgent.CashMovementType],
    amount: [0, [Validators.required, Validators.min(1)]],
    note: ['', Validators.required],
  });

  constructor() {
    effect(() => {
      const id = this.theaterCtx.currentTheaterId();
      untracked(() => {
        this.loaded.set(false);
        if (!id) {
          return;
        }
        this.drawerSvc.refresh().subscribe({
          next: () => this.loaded.set(true),
          error: () => this.loaded.set(true),
        });
      });
    });
  }

  open(): void {
    if (this.openForm.invalid) {
      this.openForm.markAllAsTouched();
      return;
    }
    const { terminalName, openingFloat } = this.openForm.getRawValue();
    this.busy.set(true);
    this.drawerSvc.open(terminalName.trim(), Math.round(openingFloat)).subscribe({
      next: () => {
        this.busy.set(false);
        this._saveTerminal(terminalName.trim());
        this._toast.success(this._translate.instant('drawer.open.done'));
      },
      error: err => this._fail(err),
    });
  }

  move(directive: FormGroupDirective): void {
    if (this.moveForm.invalid) {
      this.moveForm.markAllAsTouched();
      return;
    }
    const { type, amount, note } = this.moveForm.getRawValue();
    if (type === StaffServiceAgent.CashMovementType.PayOut) {
      // A pay-out takes cash out of the till: it needs a manager unless the caller is one.
      this._approval.request().subscribe(approval => {
        if (approval) {
          this._record(type, amount, note, directive, approval.override);
        }
      });
    } else {
      this._record(type, amount, note, directive);
    }
  }

  private _record(type: StaffServiceAgent.CashMovementType, amount: number, note: string, directive: FormGroupDirective, override?: StaffServiceAgent.ManagerOverrideDTO): void {
    this.busy.set(true);
    this.drawerSvc.payInOut(type, Math.round(amount), note.trim(), override).subscribe({
      next: () => {
        this.busy.set(false);
        // resetForm clears the directive's "submitted" state too, which is what makes mat-error show.
        directive.resetForm({ type, amount: 0, note: '' });
        this._toast.success(this._translate.instant('drawer.move.done'));
      },
      error: err => this._fail(err),
    });
  }

  private _fail(err: unknown): void {
    this.busy.set(false);
    this._toast.error(apiErrorMessage(err, this._translate.instant('drawer.failed')));
  }

  private _readTerminal(): string {
    try {
      return localStorage.getItem(TERMINAL_STORAGE_KEY) ?? '';
    } catch {
      return '';
    }
  }

  private _saveTerminal(name: string): void {
    try {
      localStorage.setItem(TERMINAL_STORAGE_KEY, name);
    } catch {
      /* storage unavailable: the name is just not remembered */
    }
  }
}

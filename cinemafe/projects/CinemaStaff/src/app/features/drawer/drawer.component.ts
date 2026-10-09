import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormBuilder, FormGroupDirective, Validators } from '@angular/forms';
import { TranslateService } from '@ngx-translate/core';
import {
  EmptyStateComponent, SharedModule, CinemaServiceAgent, ToastService, apiErrorMessage, cashMovementTypeLabel,
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
  templateUrl: './drawer.component.html',
  styleUrl: './drawer.component.scss',
})
export class DrawerComponent {
  readonly CashMovementType = CinemaServiceAgent.CashMovementType;
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
    type: [CinemaServiceAgent.CashMovementType.PayIn as CinemaServiceAgent.CashMovementType],
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
    if (type === CinemaServiceAgent.CashMovementType.PayOut) {
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

  private _record(type: CinemaServiceAgent.CashMovementType, amount: number, note: string, directive: FormGroupDirective, override?: CinemaServiceAgent.ManagerOverrideDTO): void {
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

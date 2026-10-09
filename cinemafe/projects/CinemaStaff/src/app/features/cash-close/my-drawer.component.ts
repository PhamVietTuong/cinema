import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { FormControl } from '@angular/forms';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import {
  DialogService,
  EmptyStateComponent,
  SharedModule,
  CinemaServiceAgent,
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
  templateUrl: './my-drawer.component.html',
  styleUrl: './my-drawer.component.scss',
})
export class MyDrawerComponent {
  private readonly _boxOffice = inject(CinemaServiceAgent.HttpService);
  private readonly _theater = inject(TheaterContextService);
  private readonly _store = inject(Store);
  private readonly _dialogs = inject(DialogService);
  private readonly _translate = inject(TranslateService);

  readonly theaterId = this._theater.currentTheaterId;
  readonly countControl = new FormControl<number | null>(null);
  readonly noteControl = new FormControl('');

  readonly drawer = signal<CinemaServiceAgent.CashDrawerDTO | null>(null);
  readonly result = signal<CinemaServiceAgent.CloseDrawerResultDTO | null>(null);
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
    this._boxOffice.getMyDrawer(CinemaServiceAgent.BoxOfficeScopeRequest.fromJS({ theaterId: this.theaterId() ?? undefined })).subscribe({
      next: drawer => this.drawer.set(drawer),
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }

  close(drawer: CinemaServiceAgent.CashDrawerDTO): void {
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
      this._boxOffice.closeDrawer(CinemaServiceAgent.CloseDrawerRequest.fromJS({
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

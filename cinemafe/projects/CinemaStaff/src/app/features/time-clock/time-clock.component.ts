import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { forkJoin } from 'rxjs';
import {
  APPROVER_ROLES, EmptyStateComponent, MAX_RANGE_DAYS, SharedModule, CinemaServiceAgent, addDays, hideLoading, isRangeWithin,
  parseDateKey, selectCurrentUser, showError, showException, showLoading, showSuccess, startOfDay, toDateKey, toWallClockUtc,
} from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';

/** Formats minutes as `Hh MMm`. */
export function formatMinutes(minutes: number | undefined): string {
  const total = Math.max(0, Math.round(minutes ?? 0));
  return `${Math.floor(total / 60)}h ${String(total % 60).padStart(2, '0')}m`;
}

/** Clock in/out widget, the caller's upcoming shifts, and (approvers) a theater time sheet. */
@Component({
  selector: 'staff-time-clock',
  standalone: true,
  imports: [SharedModule, EmptyStateComponent],
  templateUrl: './time-clock.component.html',
  styleUrl: './time-clock.component.scss',
})
export class TimeClockComponent implements OnInit {
  status: CinemaServiceAgent.ClockStatusDTO | null = null;
  myShifts: CinemaServiceAgent.StaffShiftDTO[] = [];
  sheet: CinemaServiceAgent.TimeClockEntryDTO[] = [];
  staff: CinemaServiceAgent.TheaterStaffDTO[] = [];
  sheetLoaded = false;
  note = '';
  busy = false;
  sheetForm: FormGroup;
  readonly format = formatMinutes;

  private readonly _workforce = inject(CinemaServiceAgent.HttpService);
  private readonly _theaterContext = inject(TheaterContextService);
  private readonly _store = inject(Store);
  private readonly _translate = inject(TranslateService);
  private readonly _cd = inject(ChangeDetectorRef);
  private readonly _user = this._store.selectSignal(selectCurrentUser);

  constructor(fb: FormBuilder) {
    const today = startOfDay(new Date());
    this.sheetForm = fb.group({ from: [toDateKey(addDays(today, -6))], to: [toDateKey(today)], userId: [''] });
    effect(() => {
      const theaterId = this._theaterContext.currentTheaterId();
      this.sheet = [];
      this.sheetLoaded = false;
      if (theaterId && this.isApprover) {
        this._loadStaff(theaterId);
      }
    });
  }

  get theaterId(): string {
    return this._theaterContext.currentTheaterId() ?? '';
  }

  get isApprover(): boolean {
    return APPROVER_ROLES.includes(this._user()?.userTypeName ?? '');
  }

  get totalMinutes(): number {
    return this.sheet.reduce((sum, entry) => sum + (entry.durationMinutes ?? 0), 0);
  }

  ngOnInit(): void {
    const today = startOfDay(new Date());
    this._store.dispatch(showLoading());
    forkJoin({
      status: this._workforce.getMyClockStatus(),
      shifts: this._workforce.getMyShifts(CinemaServiceAgent.MyShiftsRequest.fromJS({
        from: toWallClockUtc(today), to: toWallClockUtc(addDays(today, 14)),
      })),
    }).subscribe({
      next: result => {
        this.status = result.status;
        this.myShifts = result.shifts ?? [];
        this._cd.markForCheck();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this._store.dispatch(hideLoading());
      this._cd.markForCheck();
    });
  }

  clockIn(): void {
    this._act(
      this._workforce.clockIn(CinemaServiceAgent.ClockInRequest.fromJS({
        theaterId: this._theaterContext.currentTheaterId() ?? undefined, note: this.note.trim() || undefined,
      })),
      'timeClock.toast.in',
    );
  }

  clockOut(): void {
    this._act(
      this._workforce.clockOut(CinemaServiceAgent.ClockOutRequest.fromJS({ note: this.note.trim() || undefined })),
      'timeClock.toast.out',
    );
  }

  loadSheet(): void {
    const v = this.sheetForm.value;
    const from = parseDateKey(v.from);
    const toDay = parseDateKey(v.to);
    if (!from || !toDay || !this.theaterId) {
      this._store.dispatch(showError({ message: this._translate.instant('timeClock.errors.range') }));
      return;
    }
    const to = addDays(toDay, 1);
    if (!isRangeWithin(from, to, MAX_RANGE_DAYS)) {
      this._store.dispatch(showError({ message: this._translate.instant('timeClock.errors.range', { days: MAX_RANGE_DAYS }) }));
      return;
    }
    this._store.dispatch(showLoading());
    this._workforce.getTimeSheet(CinemaServiceAgent.TimeSheetRequest.fromJS({
      theaterId: this.theaterId, from: toWallClockUtc(from), to: toWallClockUtc(to), userId: v.userId || undefined,
    })).subscribe({
      next: entries => {
        this.sheet = entries ?? [];
        this.sheetLoaded = true;
        this._cd.markForCheck();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this._store.dispatch(hideLoading());
      this._cd.markForCheck();
    });
  }

  private _act(request$: ReturnType<CinemaServiceAgent.HttpService['clockIn']>, toastKey: string): void {
    this.busy = true;
    this._store.dispatch(showLoading());
    request$.subscribe({
      next: () => {
        this.note = '';
        this._store.dispatch(showSuccess({ message: this._translate.instant(toastKey) }));
        this._refreshStatus();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this.busy = false;
      this._store.dispatch(hideLoading());
      this._cd.markForCheck();
    });
  }

  private _refreshStatus(): void {
    this._workforce.getMyClockStatus().subscribe(status => {
      this.status = status;
      this._cd.markForCheck();
    });
  }

  private _loadStaff(theaterId: string): void {
    this._workforce.getTheaterStaff(CinemaServiceAgent.GetTheaterStaffRequest.fromJS({ theaterId })).subscribe({
      next: staff => {
        this.staff = staff ?? [];
        this._cd.markForCheck();
      },
      error: error => this._store.dispatch(showException({ error })),
    });
  }
}

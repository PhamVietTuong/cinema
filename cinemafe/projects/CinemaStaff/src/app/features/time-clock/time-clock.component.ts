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
  template: `
<div class="ad-page">
  <div class="ad-page-header">
    <div>
      <h1 class="ad-h1">{{ 'timeClock.title' | translate }}</h1>
      <p class="ad-sub">{{ 'timeClock.subtitle' | translate }}</p>
    </div>
  </div>

  <mat-card class="card clock">
    @if (status?.isClockedIn) {
      <div class="state state--in">
        <mat-icon>timer</mat-icon>
        <div>
          <strong>{{ 'timeClock.clockedIn' | translate }}</strong>
          <div class="muted">{{ 'timeClock.since' | translate: { time: (status?.openEntry?.clockInAt | serverUtc | date: 'dd/MM HH:mm') } }}</div>
        </div>
      </div>
    } @else {
      <div class="state">
        <mat-icon>timer_off</mat-icon>
        <strong>{{ 'timeClock.clockedOut' | translate }}</strong>
      </div>
    }
    <mat-form-field appearance="outline" subscriptSizing="dynamic" class="note">
      <mat-label>{{ 'opsCommon.note' | translate }}</mat-label>
      <input matInput maxlength="300" [value]="note" (input)="note = $any($event.target).value">
    </mat-form-field>
    @if (status?.isClockedIn) {
      <button mat-raised-button color="warn" type="button" [disabled]="busy" (click)="clockOut()"><mat-icon>logout</mat-icon> {{ 'timeClock.clockOut' | translate }}</button>
    } @else {
      <button mat-raised-button color="primary" type="button" [disabled]="busy" (click)="clockIn()"><mat-icon>login</mat-icon> {{ 'timeClock.clockIn' | translate }}</button>
    }
  </mat-card>

  <mat-card class="card ad-card--pad-0">
    <div class="head"><h3 class="ad-card-title">{{ 'timeClock.myShifts' | translate }}</h3></div>
    @if (myShifts.length) {
      <div class="ad-table-wrap">
        <table class="ad-table">
          <thead><tr><th>{{ 'opsCommon.date' | translate }}</th><th>{{ 'timeClock.time' | translate }}</th><th>{{ 'opsCommon.note' | translate }}</th></tr></thead>
          <tbody>
            @for (shift of myShifts; track shift.id) {
              <tr>
                <td>{{ shift.startTime | date: 'EEE dd/MM/yyyy' }}</td>
                <td>{{ shift.startTime | date: 'HH:mm' }} - {{ shift.endTime | date: 'HH:mm' }}</td>
                <td>{{ shift.note }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    } @else {
      <cl-empty-state icon="event_busy" messageKey="timeClock.noShifts" />
    }
  </mat-card>

  @if (isApprover) {
    <mat-card class="card">
      <h3 class="ad-card-title">{{ 'timeClock.timeSheet' | translate }}</h3>
      @if (!theaterId) {
        <cl-empty-state icon="theaters" messageKey="opsCommon.pickTheater" hintKey="opsCommon.pickTheaterHint" />
      } @else {
        <form [formGroup]="sheetForm" class="filters">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ 'opsCommon.from' | translate }}</mat-label>
            <input matInput type="date" formControlName="from">
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ 'opsCommon.to' | translate }}</mat-label>
            <input matInput type="date" formControlName="to">
          </mat-form-field>
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ 'roster.staff' | translate }}</mat-label>
            <mat-select formControlName="userId">
              <mat-option value="">{{ 'common.all' | translate }}</mat-option>
              @for (member of staff; track member.id) {
                <mat-option [value]="member.id">{{ member.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <button mat-stroked-button type="button" (click)="loadSheet()"><mat-icon>search</mat-icon> {{ 'timeClock.load' | translate }}</button>
        </form>
        @if (sheet.length) {
          <div class="ad-table-wrap">
            <table class="ad-table">
              <thead>
                <tr>
                  <th>{{ 'roster.staff' | translate }}</th><th>{{ 'timeClock.in' | translate }}</th><th>{{ 'timeClock.out' | translate }}</th>
                  <th class="num">{{ 'timeClock.duration' | translate }}</th>
                </tr>
              </thead>
              <tbody>
                @for (entry of sheet; track entry.id) {
                  <tr>
                    <td>{{ entry.userName }}</td>
                    <td>{{ entry.clockInAt | serverUtc | date: 'dd/MM HH:mm' }}</td>
                    <td>{{ entry.clockOutAt ? (entry.clockOutAt | serverUtc | date: 'dd/MM HH:mm') : ('timeClock.open' | translate) }}</td>
                    <td class="num">{{ format(entry.durationMinutes) }}</td>
                  </tr>
                }
              </tbody>
              <tfoot><tr><td colspan="3"><strong>{{ 'timeClock.total' | translate }}</strong></td><td class="num"><strong>{{ format(totalMinutes) }}</strong></td></tr></tfoot>
            </table>
          </div>
        } @else if (sheetLoaded) {
          <cl-empty-state icon="schedule" messageKey="timeClock.sheetEmpty" />
        }
      }
    </mat-card>
  }
</div>
`,
  styles: [`
    .card { padding: 16px; margin-bottom: 16px; }
    .card.ad-card--pad-0 { padding: 0; }
    .head { padding: 16px 16px 0; }
    .clock { display: flex; align-items: center; gap: 16px; flex-wrap: wrap; }
    .state { display: flex; align-items: center; gap: 12px; min-width: 200px; }
    .state mat-icon { font-size: 32px; width: 32px; height: 32px; color: var(--ml-muted); }
    .state--in mat-icon { color: var(--ml-success-ink); }
    .note { flex: 1 1 240px; }
    .muted { color: var(--ml-muted); font-size: 12px; }
    .filters { display: flex; gap: 12px; flex-wrap: wrap; align-items: center; margin-bottom: 12px; }
    .num { text-align: right; }
  `],
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

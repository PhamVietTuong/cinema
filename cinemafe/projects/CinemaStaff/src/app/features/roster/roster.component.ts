import { ChangeDetectorRef, Component, effect, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Store } from '@ngrx/store';
import { forkJoin } from 'rxjs';
import {
  EmptyStateComponent, MAX_RANGE_DAYS, SharedModule, CinemaServiceAgent, addDays, hideLoading, isRangeWithin,
  showException, showLoading, startOfWeek, toWallClockUtc, weekDays,
} from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';
import { ShiftDialog } from './shift.dialog';
import { shiftsOnDay, totalShiftHours } from './shift-times';

/** Week roster for approvers: one row per staff member, one column per day; click a cell to add a shift, a chip to edit it. */
@Component({
  selector: 'staff-roster',
  standalone: true,
  imports: [SharedModule, EmptyStateComponent],
  templateUrl: './roster.component.html',
  styleUrl: './roster.component.scss',
})
export class RosterComponent {
  weekStart = startOfWeek(new Date());
  days = weekDays(this.weekStart);
  staff: CinemaServiceAgent.TheaterStaffDTO[] = [];
  shifts: CinemaServiceAgent.StaffShiftDTO[] = [];
  loading = false;

  private readonly _workforce = inject(CinemaServiceAgent.HttpService);
  private readonly _theaterContext = inject(TheaterContextService);
  private readonly _store = inject(Store);
  private readonly _dialog = inject(MatDialog);
  private readonly _cd = inject(ChangeDetectorRef);

  constructor() {
    effect(() => {
      this._theaterContext.currentTheaterId();
      this.load();
    });
  }

  get theaterId(): string {
    return this._theaterContext.currentTheaterId() ?? '';
  }

  shiftWeek(delta: number): void {
    this._setWeek(addDays(this.weekStart, delta * 7));
  }

  thisWeek(): void {
    this._setWeek(startOfWeek(new Date()));
  }

  shiftsFor(userId: string, day: Date): CinemaServiceAgent.StaffShiftDTO[] {
    return shiftsOnDay(this.shifts, userId, day);
  }

  hoursFor(userId: string): number {
    return totalShiftHours(this.shifts.filter(shift => shift.userId === userId));
  }

  addShift(member: CinemaServiceAgent.TheaterStaffDTO, day: Date): void {
    this._openDialog({ theaterId: this.theaterId, userId: member.id!, userName: member.name ?? '', day });
  }

  editShift(member: CinemaServiceAgent.TheaterStaffDTO, shift: CinemaServiceAgent.StaffShiftDTO, event: Event): void {
    event.stopPropagation();
    this._openDialog({ theaterId: this.theaterId, userId: member.id!, userName: member.name ?? '', day: shift.startTime!, shift });
  }

  load(): void {
    const theaterId = this.theaterId;
    const from = this.weekStart;
    const to = addDays(from, 7);
    if (!theaterId || !isRangeWithin(from, to, MAX_RANGE_DAYS)) {
      this.staff = [];
      this.shifts = [];
      this._cd.markForCheck();
      return;
    }
    this.loading = true;
    this._store.dispatch(showLoading());
    forkJoin({
      staff: this._workforce.getTheaterStaff(CinemaServiceAgent.GetTheaterStaffRequest.fromJS({ theaterId })),
      shifts: this._workforce.getRoster(CinemaServiceAgent.RosterRequest.fromJS({
        theaterId, from: toWallClockUtc(from), to: toWallClockUtc(to),
      })),
    }).subscribe({
      next: result => {
        this.staff = result.staff ?? [];
        this.shifts = result.shifts ?? [];
        this._cd.markForCheck();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this.loading = false;
      this._store.dispatch(hideLoading());
      this._cd.markForCheck();
    });
  }

  private _setWeek(start: Date): void {
    this.weekStart = start;
    this.days = weekDays(start);
    this.load();
  }

  private _openDialog(data: { theaterId: string; userId: string; userName: string; day: Date; shift?: CinemaServiceAgent.StaffShiftDTO }): void {
    this._dialog.open(ShiftDialog, { width: '420px', maxWidth: '95vw', data }).afterClosed().subscribe(changed => {
      if (changed) {
        this.load();
      }
    });
  }
}

import { ChangeDetectorRef, Component, effect, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Store } from '@ngrx/store';
import { forkJoin } from 'rxjs';
import {
  EmptyStateComponent, MAX_RANGE_DAYS, SharedModule, StaffServiceAgent, addDays, hideLoading, isRangeWithin,
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
  template: `
<div class="ad-page">
  <div class="ad-page-header">
    <div>
      <h1 class="ad-h1">{{ 'roster.title' | translate }}</h1>
      <p class="ad-sub">{{ 'roster.subtitle' | translate }}</p>
    </div>
    <div class="ad-toolbar nav">
      <button mat-icon-button type="button" [attr.aria-label]="'roster.prevWeek' | translate" (click)="shiftWeek(-1)"><mat-icon>chevron_left</mat-icon></button>
      <strong>{{ weekStart | date: 'dd/MM' }} - {{ days[6] | date: 'dd/MM/yyyy' }}</strong>
      <button mat-icon-button type="button" [attr.aria-label]="'roster.nextWeek' | translate" (click)="shiftWeek(1)"><mat-icon>chevron_right</mat-icon></button>
      <button mat-stroked-button type="button" (click)="thisWeek()">{{ 'roster.thisWeek' | translate }}</button>
    </div>
  </div>

  @if (!theaterId) {
    <mat-card class="ad-card--pad-0">
      <cl-empty-state icon="theaters" messageKey="opsCommon.pickTheater" hintKey="opsCommon.pickTheaterHint" />
    </mat-card>
  } @else {
    <mat-card class="ad-card--pad-0">
      @if (!staff.length && !loading) {
        <cl-empty-state icon="groups" messageKey="roster.empty" />
      } @else {
        <div class="scroll">
          <table class="ad-table grid">
            <thead>
              <tr>
                <th>{{ 'roster.staff' | translate }}</th>
                @for (day of days; track day.getTime()) {
                  <th>{{ day | date: 'EEE dd/MM' }}</th>
                }
                <th class="num">{{ 'roster.hours' | translate }}</th>
              </tr>
            </thead>
            <tbody>
              @for (member of staff; track member.id) {
                <tr>
                  <td><strong>{{ member.name }}</strong><br><span class="muted">{{ member.roleName }}</span></td>
                  @for (day of days; track day.getTime()) {
                    <td class="cell" (click)="addShift(member, day)">
                      @for (shift of shiftsFor(member.id!, day); track shift.id) {
                        <button type="button" class="chip" (click)="editShift(member, shift, $event)">
                          {{ shift.startTime | date: 'HH:mm' }}-{{ shift.endTime | date: 'HH:mm' }}
                        </button>
                      }
                    </td>
                  }
                  <td class="num">{{ hoursFor(member.id!) }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </mat-card>
  }
</div>
`,
  styles: [`
    .nav { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
    .scroll { overflow-x: auto; }
    .grid { min-width: 900px; }
    .cell { cursor: pointer; vertical-align: top; min-width: 100px; }
    .cell:hover { background: var(--ml-panel-3, rgba(0, 0, 0, 0.04)); }
    .chip { display: block; width: 100%; margin-bottom: 4px; border: 0; border-radius: 4px; padding: 3px 6px; cursor: pointer; font-size: 12px;
      background: var(--ml-action-soft); color: var(--ml-action-strong); }
    .muted { color: var(--ml-muted); font-size: 12px; }
    .num { text-align: right; }
  `],
})
export class RosterComponent {
  weekStart = startOfWeek(new Date());
  days = weekDays(this.weekStart);
  staff: StaffServiceAgent.TheaterStaffDTO[] = [];
  shifts: StaffServiceAgent.StaffShiftDTO[] = [];
  loading = false;

  private readonly _workforce = inject(StaffServiceAgent.WorkforceHttpService);
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

  shiftsFor(userId: string, day: Date): StaffServiceAgent.StaffShiftDTO[] {
    return shiftsOnDay(this.shifts, userId, day);
  }

  hoursFor(userId: string): number {
    return totalShiftHours(this.shifts.filter(shift => shift.userId === userId));
  }

  addShift(member: StaffServiceAgent.TheaterStaffDTO, day: Date): void {
    this._openDialog({ theaterId: this.theaterId, userId: member.id!, userName: member.name ?? '', day });
  }

  editShift(member: StaffServiceAgent.TheaterStaffDTO, shift: StaffServiceAgent.StaffShiftDTO, event: Event): void {
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
      staff: this._workforce.getTheaterStaff(StaffServiceAgent.GetTheaterStaffRequest.fromJS({ theaterId })),
      shifts: this._workforce.getRoster(StaffServiceAgent.RosterRequest.fromJS({
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

  private _openDialog(data: { theaterId: string; userId: string; userName: string; day: Date; shift?: StaffServiceAgent.StaffShiftDTO }): void {
    this._dialog.open(ShiftDialog, { width: '420px', maxWidth: '95vw', data }).afterClosed().subscribe(changed => {
      if (changed) {
        this.load();
      }
    });
  }
}

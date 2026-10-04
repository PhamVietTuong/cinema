import { ChangeDetectorRef, Component, effect, inject } from '@angular/core';
import { FormControl } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import {
  APPROVER_ROLES, EmptyStateComponent, SharedModule, StaffServiceAgent, StatusPillComponent, selectCurrentUser,
  addDays, hideLoading, parseDateKey, showException, showLoading, startOfDay, toDateKey, toWallClockUtc,
} from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';

/** Entry page of the checklists: the day's showtimes per room, each with a pre-show and a post-show checklist button. */
@Component({
  selector: 'staff-checklists-home',
  standalone: true,
  imports: [SharedModule, StatusPillComponent, EmptyStateComponent],
  template: `
<div class="ad-page">
  <div class="ad-page-header">
    <div>
      <h1 class="ad-h1">{{ 'checklists.title' | translate }}</h1>
      <p class="ad-sub">{{ 'checklists.subtitle' | translate }}</p>
    </div>
    <div class="ad-toolbar day-nav">
      <button mat-icon-button type="button" [attr.aria-label]="'schedule.prevDay' | translate" (click)="shiftDay(-1)"><mat-icon>chevron_left</mat-icon></button>
      <input class="ad-input" type="date" [formControl]="dateControl" (change)="load()" [attr.aria-label]="'opsCommon.date' | translate">
      <button mat-icon-button type="button" [attr.aria-label]="'schedule.nextDay' | translate" (click)="shiftDay(1)"><mat-icon>chevron_right</mat-icon></button>
      @if (isApprover) {
        <button mat-stroked-button type="button" (click)="openTemplates()"><mat-icon>edit_note</mat-icon> {{ 'checklists.templates.open' | translate }}</button>
      }
    </div>
  </div>

  @if (!theaterId) {
    <mat-card class="ad-card--pad-0">
      <cl-empty-state icon="theaters" messageKey="opsCommon.pickTheater" hintKey="opsCommon.pickTheaterHint" />
    </mat-card>
  } @else {
    @for (room of rooms; track room.roomId) {
      @if (room.showTimes?.length) {
        <mat-card class="ad-card--pad-0 room-card">
          <div class="room-head">
            <strong>{{ room.roomName }}</strong>
            <cl-status-pill kind="roomStatus" [value]="room.roomStatus" />
          </div>
          <div class="ad-table-wrap">
            <table class="ad-table">
              <thead>
                <tr>
                  <th>{{ 'checklists.home.time' | translate }}</th>
                  <th>{{ 'checklists.home.movie' | translate }}</th>
                  <th class="actions">{{ 'common.actions' | translate }}</th>
                </tr>
              </thead>
              <tbody>
                @for (st of room.showTimes; track st.showTimeId) {
                  <tr>
                    <td>{{ st.start | date: 'HH:mm' }} - {{ st.end | date: 'HH:mm' }}</td>
                    <td>{{ st.movieTitle }}</td>
                    <td class="actions">
                      <button mat-stroked-button type="button" (click)="run(st, room, Kind.PreShow)">{{ 'staffEnums.checklistKind.preShow' | translate }}</button>
                      <button mat-stroked-button type="button" (click)="run(st, room, Kind.PostShow)">{{ 'staffEnums.checklistKind.postShow' | translate }}</button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </mat-card>
      }
    }
    @if (!hasShowTimes && !loading) {
      <mat-card class="ad-card--pad-0">
        <cl-empty-state icon="fact_check" messageKey="checklists.home.empty" />
      </mat-card>
    }
  }
</div>
`,
  styles: [`
    .day-nav { display: flex; align-items: center; gap: 4px; flex-wrap: wrap; }
    .room-card { margin-bottom: 16px; }
    .room-head { display: flex; align-items: center; gap: 12px; padding: 12px 16px 0; }
    .actions { text-align: right; white-space: nowrap; }
    .actions button + button { margin-left: 8px; }
  `],
})
export class ChecklistsHomeComponent {
  readonly Kind = StaffServiceAgent.ChecklistKind;
  readonly dateControl = new FormControl<string>(toDateKey(new Date()), { nonNullable: true });

  rooms: StaffServiceAgent.ScheduleBoardRoomDTO[] = [];
  loading = false;

  private readonly _ops = inject(StaffServiceAgent.OperationsHttpService);
  private readonly _theaterContext = inject(TheaterContextService);
  private readonly _store = inject(Store);
  private readonly _router = inject(Router);
  private readonly _cd = inject(ChangeDetectorRef);
  private readonly _user = this._store.selectSignal(selectCurrentUser);

  constructor() {
    effect(() => {
      this._theaterContext.currentTheaterId();
      this.load();
    });
  }

  get theaterId(): string {
    return this._theaterContext.currentTheaterId() ?? '';
  }

  get isApprover(): boolean {
    return APPROVER_ROLES.includes(this._user()?.userTypeName ?? '');
  }

  get hasShowTimes(): boolean {
    return this.rooms.some(room => (room.showTimes?.length ?? 0) > 0);
  }

  shiftDay(delta: number): void {
    const current = parseDateKey(this.dateControl.value) ?? startOfDay(new Date());
    this.dateControl.setValue(toDateKey(addDays(current, delta)));
    this.load();
  }

  load(): void {
    const theaterId = this.theaterId;
    const day = parseDateKey(this.dateControl.value);
    if (!theaterId || !day) {
      this.rooms = [];
      this._cd.markForCheck();
      return;
    }
    this.loading = true;
    this._store.dispatch(showLoading());
    this._ops.getScheduleBoard(StaffServiceAgent.ScheduleBoardRequest.fromJS({
      theaterId, date: toWallClockUtc(day),
    })).subscribe({
      next: board => {
        this.rooms = board.rooms ?? [];
        this._cd.markForCheck();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this.loading = false;
      this._store.dispatch(hideLoading());
      this._cd.markForCheck();
    });
  }

  run(showTime: StaffServiceAgent.ScheduleBoardShowTimeDTO, room: StaffServiceAgent.ScheduleBoardRoomDTO, kind: StaffServiceAgent.ChecklistKind): void {
    this._router.navigate(['/checklists/run', showTime.showTimeId, room.roomId], { queryParams: { kind } });
  }

  openTemplates(): void {
    this._router.navigate(['/checklists/templates']);
  }
}

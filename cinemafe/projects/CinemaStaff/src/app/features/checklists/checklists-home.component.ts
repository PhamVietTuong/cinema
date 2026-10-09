import { ChangeDetectorRef, Component, effect, inject } from '@angular/core';
import { FormControl } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import {
  APPROVER_ROLES, EmptyStateComponent, SharedModule, CinemaServiceAgent, StatusPillComponent, selectCurrentUser,
  addDays, hideLoading, parseDateKey, showException, showLoading, startOfDay, toDateKey, toWallClockUtc,
} from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';

/** Entry page of the checklists: the day's showtimes per room, each with a pre-show and a post-show checklist button. */
@Component({
  selector: 'staff-checklists-home',
  standalone: true,
  imports: [SharedModule, StatusPillComponent, EmptyStateComponent],
  templateUrl: './checklists-home.component.html',
  styleUrl: './checklists-home.component.scss',
})
export class ChecklistsHomeComponent {
  readonly Kind = CinemaServiceAgent.ChecklistKind;
  readonly dateControl = new FormControl<string>(toDateKey(new Date()), { nonNullable: true });

  rooms: CinemaServiceAgent.ScheduleBoardRoomDTO[] = [];
  loading = false;

  private readonly _ops = inject(CinemaServiceAgent.HttpService);
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
    this._ops.getScheduleBoard(CinemaServiceAgent.ScheduleBoardRequest.fromJS({
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

  run(showTime: CinemaServiceAgent.ScheduleBoardShowTimeDTO, room: CinemaServiceAgent.ScheduleBoardRoomDTO, kind: CinemaServiceAgent.ChecklistKind): void {
    this._router.navigate(['/checklists/run', showTime.showTimeId, room.roomId], { queryParams: { kind } });
  }

  openTemplates(): void {
    this._router.navigate(['/checklists/templates']);
  }
}

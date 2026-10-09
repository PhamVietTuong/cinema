import { ChangeDetectorRef, Component, effect, inject } from '@angular/core';
import { FormControl } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import {
  EmptyStateComponent, SharedModule, CinemaServiceAgent, StatusPillComponent,
  addDays, hideLoading, parseDateKey, showException, showLoading, startOfDay, toDateKey, toWallClockUtc,
} from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';
import { TimelineBlock, TimelineTick, TimelineWindow, computeWindow, hourTicks, layoutShowTime, soldPercent } from './timeline-layout';

interface BoardShowTime {
  source: CinemaServiceAgent.ScheduleBoardShowTimeDTO;
  block: TimelineBlock;
  soldPct: number;
}

interface BoardRoom {
  source: CinemaServiceAgent.ScheduleBoardRoomDTO;
  showTimes: BoardShowTime[];
}

/** Per-room timeline of one day: showtime blocks, the turnover buffer after each, sold/capacity and the room status. */
@Component({
  selector: 'staff-schedule-board',
  standalone: true,
  imports: [SharedModule, StatusPillComponent, EmptyStateComponent],
  templateUrl: './schedule-board.component.html',
  styleUrl: './schedule-board.component.scss',
})
export class ScheduleBoardComponent {
  readonly dateControl = new FormControl<string>(toDateKey(new Date()), { nonNullable: true });

  rooms: BoardRoom[] = [];
  ticks: TimelineTick[] = [];
  loading = false;

  private readonly _ops = inject(CinemaServiceAgent.HttpService);
  private readonly _theaterContext = inject(TheaterContextService);
  private readonly _store = inject(Store);
  private readonly _router = inject(Router);
  private readonly _cd = inject(ChangeDetectorRef);

  constructor() {
    // Reload when an Admin switches the topbar theater.
    effect(() => {
      this._theaterContext.currentTheaterId();
      this.load();
    });
  }

  get theaterId(): string {
    return this._theaterContext.currentTheaterId() ?? '';
  }

  today(): void {
    this.dateControl.setValue(toDateKey(new Date()));
    this.load();
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
      this.ticks = [];
      this._cd.markForCheck();
      return;
    }
    this.loading = true;
    this._store.dispatch(showLoading());
    this._ops.getScheduleBoard(CinemaServiceAgent.ScheduleBoardRequest.fromJS({
      theaterId, date: toWallClockUtc(day),
    })).subscribe({
      next: board => {
        this._apply(board, day);
        this._cd.markForCheck();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this.loading = false;
      this._store.dispatch(hideLoading());
      this._cd.markForCheck();
    });
  }

  openChecklist(showTime: CinemaServiceAgent.ScheduleBoardShowTimeDTO, room: CinemaServiceAgent.ScheduleBoardRoomDTO): void {
    this._router.navigate(['/checklists/run', showTime.showTimeId, room.roomId], {
      queryParams: { kind: CinemaServiceAgent.ChecklistKind.PreShow },
    });
  }

  private _apply(board: CinemaServiceAgent.ScheduleBoardDTO, day: Date): void {
    const all = (board.rooms ?? []).flatMap(room => room.showTimes ?? []);
    const window: TimelineWindow = computeWindow(all.map(s => ({ start: s.start!, end: s.end!, bufferEnd: s.bufferEnd! })), day);
    this.ticks = hourTicks(window);
    this.rooms = (board.rooms ?? []).map(room => ({
      source: room,
      showTimes: (room.showTimes ?? []).map(source => ({
        source,
        block: layoutShowTime({ start: source.start!, end: source.end!, bufferEnd: source.bufferEnd! }, window),
        soldPct: soldPercent(source.sold, source.capacity),
      })),
    }));
  }
}

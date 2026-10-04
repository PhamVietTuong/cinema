import { ChangeDetectorRef, Component, effect, inject } from '@angular/core';
import { FormControl } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import {
  EmptyStateComponent, SharedModule, StaffServiceAgent, StatusPillComponent,
  addDays, hideLoading, parseDateKey, showException, showLoading, startOfDay, toDateKey, toWallClockUtc,
} from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';
import { TimelineBlock, TimelineTick, TimelineWindow, computeWindow, hourTicks, layoutShowTime, soldPercent } from './timeline-layout';

interface BoardShowTime {
  source: StaffServiceAgent.ScheduleBoardShowTimeDTO;
  block: TimelineBlock;
  soldPct: number;
}

interface BoardRoom {
  source: StaffServiceAgent.ScheduleBoardRoomDTO;
  showTimes: BoardShowTime[];
}

/** Per-room timeline of one day: showtime blocks, the turnover buffer after each, sold/capacity and the room status. */
@Component({
  selector: 'staff-schedule-board',
  standalone: true,
  imports: [SharedModule, StatusPillComponent, EmptyStateComponent],
  template: `
<div class="ad-page">
  <div class="ad-page-header">
    <div>
      <h1 class="ad-h1">{{ 'schedule.title' | translate }}</h1>
      <p class="ad-sub">{{ 'schedule.subtitle' | translate }}</p>
    </div>
    <div class="ad-toolbar day-nav">
      <button mat-icon-button type="button" [attr.aria-label]="'schedule.prevDay' | translate" (click)="shiftDay(-1)"><mat-icon>chevron_left</mat-icon></button>
      <input class="ad-input" type="date" [formControl]="dateControl" (change)="load()" [attr.aria-label]="'opsCommon.date' | translate">
      <button mat-icon-button type="button" [attr.aria-label]="'schedule.nextDay' | translate" (click)="shiftDay(1)"><mat-icon>chevron_right</mat-icon></button>
      <button mat-stroked-button type="button" (click)="today()">{{ 'schedule.today' | translate }}</button>
      <button mat-icon-button type="button" [attr.aria-label]="'opsCommon.refresh' | translate" (click)="load()"><mat-icon>refresh</mat-icon></button>
    </div>
  </div>

  @if (!theaterId) {
    <mat-card class="ad-card--pad-0">
      <cl-empty-state icon="theaters" messageKey="opsCommon.pickTheater" hintKey="opsCommon.pickTheaterHint" />
    </mat-card>
  } @else {
    <div class="legend">
      <span><i class="swatch swatch--show"></i> {{ 'schedule.legend.showtime' | translate }}</span>
      <span><i class="swatch swatch--buffer"></i> {{ 'schedule.legend.buffer' | translate }}</span>
    </div>

    <mat-card class="ad-card--pad-0">
      @if (!rooms.length && !loading) {
        <cl-empty-state icon="movie" messageKey="schedule.empty" />
      }
      @if (rooms.length) {
        <div class="board-scroll">
          <div class="board">
            <div class="board-row board-head">
              <div class="room-cell"></div>
              <div class="track track--head">
                @for (tick of ticks; track tick.label) {
                  <span class="tick-label" [style.left.%]="tick.leftPct">{{ tick.label }}</span>
                }
              </div>
            </div>
            @for (room of rooms; track room.source.roomId) {
              <div class="board-row">
                <div class="room-cell">
                  <strong>{{ room.source.roomName }}</strong>
                  <span class="muted">{{ room.source.roomTypeName }} · {{ room.source.capacity }} {{ 'schedule.seats' | translate }}</span>
                  <cl-status-pill kind="roomStatus" [value]="room.source.roomStatus" />
                </div>
                <div class="track">
                  @for (tick of ticks; track tick.label) {
                    <span class="grid-line" [style.left.%]="tick.leftPct"></span>
                  }
                  @for (st of room.showTimes; track st.source.showTimeId) {
                    <span class="buffer" [style.left.%]="st.block.bufferLeftPct" [style.width.%]="st.block.bufferWidthPct"
                      [title]="'schedule.bufferTitle' | translate: { minutes: room.source.turnoverBufferMinutes }"></span>
                    <button type="button" class="show" [style.left.%]="st.block.leftPct" [style.width.%]="st.block.widthPct"
                      [title]="'schedule.openChecklist' | translate"
                      (click)="openChecklist(st.source, room.source)">
                      <strong class="show-title">{{ st.source.movieTitle }}</strong>
                      <span class="show-time">{{ st.source.start | date: 'HH:mm' }} - {{ st.source.end | date: 'HH:mm' }}</span>
                      <span class="show-sold">{{ st.source.sold }}/{{ st.source.capacity }}</span>
                      <span class="fill" [style.width.%]="st.soldPct"></span>
                    </button>
                  }
                </div>
              </div>
            }
          </div>
        </div>
      }
    </mat-card>
  }
</div>
`,
  styles: [`
    .day-nav { display: flex; align-items: center; gap: 4px; flex-wrap: wrap; }
    .legend { display: flex; gap: 16px; margin-bottom: 8px; color: var(--ml-muted); font-size: 13px; }
    .swatch { display: inline-block; width: 14px; height: 10px; border-radius: 2px; vertical-align: middle; }
    .swatch--show { background: var(--ml-action-strong); }
    .swatch--buffer { background: repeating-linear-gradient(45deg, var(--ml-warn-soft), var(--ml-warn-soft) 3px, transparent 3px, transparent 6px); border: 1px solid var(--ml-warn-ink); }
    .board-scroll { overflow-x: auto; }
    .board { min-width: 980px; }
    .board-row { display: grid; grid-template-columns: 180px 1fr; border-bottom: 1px solid var(--ml-line, rgba(128, 128, 128, 0.2)); }
    .board-head { position: sticky; top: 0; }
    .room-cell { padding: 10px 12px; display: flex; flex-direction: column; gap: 4px; align-items: flex-start; }
    .muted { color: var(--ml-muted); font-size: 12px; }
    .track { position: relative; height: 72px; margin-right: 12px; }
    .track--head { height: 28px; }
    .tick-label { position: absolute; top: 6px; transform: translateX(-50%); font-size: 11px; color: var(--ml-muted); }
    .grid-line { position: absolute; top: 0; bottom: 0; width: 1px; background: var(--ml-line, rgba(128, 128, 128, 0.2)); }
    .show { position: absolute; top: 8px; height: 56px; border: 0; border-radius: 6px; padding: 4px 8px; text-align: left; cursor: pointer; overflow: hidden;
      background: var(--ml-action-strong); color: #fff; display: flex; flex-direction: column; justify-content: center; gap: 1px; min-width: 24px; }
    .show:hover { filter: brightness(1.1); }
    .show-title { font-size: 12px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .show-time, .show-sold { font-size: 11px; opacity: 0.9; white-space: nowrap; }
    .fill { position: absolute; left: 0; bottom: 0; height: 3px; background: rgba(255, 255, 255, 0.85); }
    .buffer { position: absolute; top: 8px; height: 56px; border-radius: 0 6px 6px 0; box-sizing: border-box;
      background: repeating-linear-gradient(45deg, var(--ml-warn-soft), var(--ml-warn-soft) 4px, transparent 4px, transparent 8px);
      border: 1px dashed var(--ml-warn-ink); border-left: 0; }
  `],
})
export class ScheduleBoardComponent {
  readonly dateControl = new FormControl<string>(toDateKey(new Date()), { nonNullable: true });

  rooms: BoardRoom[] = [];
  ticks: TimelineTick[] = [];
  loading = false;

  private readonly _ops = inject(StaffServiceAgent.OperationsHttpService);
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
    this._ops.getScheduleBoard(StaffServiceAgent.ScheduleBoardRequest.fromJS({
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

  openChecklist(showTime: StaffServiceAgent.ScheduleBoardShowTimeDTO, room: StaffServiceAgent.ScheduleBoardRoomDTO): void {
    this._router.navigate(['/checklists/run', showTime.showTimeId, room.roomId], {
      queryParams: { kind: StaffServiceAgent.ChecklistKind.PreShow },
    });
  }

  private _apply(board: StaffServiceAgent.ScheduleBoardDTO, day: Date): void {
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

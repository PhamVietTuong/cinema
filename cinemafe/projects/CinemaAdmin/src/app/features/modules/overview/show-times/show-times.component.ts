import { ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Subject, takeUntil } from 'rxjs';
import { CinemaServiceAgent, ProjectionFormValues, ShowTimeTypeValues } from 'CinemaLib';
import { ShowTimeDialog } from './show-time.dialog';

type Dto = CinemaServiceAgent.ShowTimeDTO;

/** A showtime positioned inside a day column of the timetable. */
interface PlacedBlock {
  st: Dto;
  top: number;       // px from the top of the grid body
  height: number;    // px
  title: string;
  timeLabel: string; // "14:30 – 16:45"
  formLabel: string; // 2D / 3D / IMAX
  typeClass: string; // st-block--normal | --premiere | --special
  bufferTop: number;     // px from the top of the grid body; equals top + height
  bufferHeight: number;  // px; 0 when the room's class has no turnover buffer (renders nothing)
  bufferMinutes: number; // the configured buffer, for the tooltip (even when bufferHeight is clipped)
}

interface DayColumn {
  date: Date;
  dow: string;   // "Th 2"
  dom: number;   // day-of-month
  isToday: boolean;
  blocks: PlacedBlock[];
}

@Component({
  selector: 'app-show-times',
  standalone: false,
  templateUrl: './show-times.component.html',
  styleUrl: './show-times.component.scss',
})
export class ShowTimesManagementComponent implements OnInit, OnDestroy {
  private _svc = inject(CinemaServiceAgent.HttpService);
  private _cdr = inject(ChangeDetectorRef);
  private _dialog = inject(MatDialog);
  private _destroy$ = new Subject<void>();

  // ── Grid geometry ───────────────────────────────────────────────────────────
  readonly startHour = 8;          // first row label
  readonly endHour = 24;           // last row boundary (exclusive label at 24:00)
  readonly rowHeight = 60;         // px per hour
  readonly hours: number[] = Array.from({ length: this.endHour - this.startHour }, (_, i) => this.startHour + i);
  get gridHeight(): number { return (this.endHour - this.startHour) * this.rowHeight; }

  // ── Lookups ─────────────────────────────────────────────────────────────────
  movies: CinemaServiceAgent.MovieDTO[] = [];
  rooms: CinemaServiceAgent.RoomDTO[] = [];
  theaters: CinemaServiceAgent.TheaterDTO[] = [];
  readonly showTimeTypes = ShowTimeTypeValues;
  readonly projectionForms = ProjectionFormValues;

  // ── Week state ────────────────────────────────────────────────────────────────
  weekStart!: Date;               // Monday 00:00 of the visible week
  days: DayColumn[] = [];
  filterMovieId = '';

  private _showtimes: Dto[] = [];

  ngOnInit(): void {
    this.weekStart = this._mondayOf(new Date());
    const wide = CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200 });
    this._svc.getMovies(wide).pipe(takeUntil(this._destroy$))
      .subscribe(r => { this.movies = r.results ?? []; this._cdr.markForCheck(); });
    this._svc.getTheaters(wide).pipe(takeUntil(this._destroy$))
      .subscribe(r => { this.theaters = r.results ?? []; this._cdr.markForCheck(); });
    this._svc.getRooms(wide).pipe(takeUntil(this._destroy$))
      .subscribe(r => { this.rooms = r.results ?? []; this._cdr.markForCheck(); });
    this.load();
  }

  ngOnDestroy(): void {
    this._destroy$.next();
    this._destroy$.complete();
  }

  // ── Data ────────────────────────────────────────────────────────────────────
  load(): void {
    // Fetch only the visible week; the backend filters on StartTime so the page stays
    // small no matter how many showtimes exist overall.
    this._svc.getShowTimeList(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: 1,
      pageSize: 200,
      filters: {
        from: this._isoLocal(this.weekStart),
        to: this._isoLocal(this._addDays(this.weekStart, 7)),
      },
    }))
      .pipe(takeUntil(this._destroy$))
      .subscribe(r => {
        this._showtimes = r.results ?? [];
        this._rebuild();
      });
  }

  private _rebuild(): void {
    const weekEnd = this._addDays(this.weekStart, 7);
    this.days = Array.from({ length: 7 }, (_, i) => {
      const date = this._addDays(this.weekStart, i);
      return {
        date,
        dow: this._dow(date),
        dom: date.getDate(),
        isToday: this._sameDay(date, new Date()),
        blocks: [] as PlacedBlock[],
      };
    });

    for (const st of this._showtimes) {
      if (!st.startTime || !st.endTime) { continue; }
      if (this.filterMovieId && st.movieId !== this.filterMovieId) { continue; }
      const start = new Date(st.startTime);
      const end = new Date(st.endTime);
      if (start < this.weekStart || start >= weekEnd) { continue; }

      const col = this.days.find(d => this._sameDay(d.date, start));
      if (!col) { continue; }
      col.blocks.push(this._place(st, start, end));
    }
    this._cdr.markForCheck();
  }

  private _place(st: Dto, start: Date, end: Date): PlacedBlock {
    const top = Math.max(0, (this._hoursFromStart(start)) * this.rowHeight);
    const rawHeight = (this._hoursFromStart(end) - this._hoursFromStart(start)) * this.rowHeight;
    const height = Math.max(28, rawHeight);

    // rowHeight is 60px/hour, i.e. exactly 1px per minute, so the buffer's minute count doubles
    // as its pixel height. Anchored to the block's rendered bottom (top + height), not recomputed
    // from st.endTime, so it stays glued to the block even when height was clamped to the 28px
    // minimum or the showtime crosses midnight.
    const bufferMinutes = st.turnoverBufferMinutes ?? 0;
    const bufferTop = top + height;
    const bufferHeight = bufferMinutes > 0 ? Math.max(0, Math.min(bufferMinutes, this.gridHeight - bufferTop)) : 0;

    return {
      st,
      top,
      height,
      title: this.movieTitle(st.movieId),
      timeLabel: `${this._hm(start)} – ${this._hm(end)}`,
      formLabel: this.formLabel(st.projectionForm),
      typeClass: this.showTimeTypes.find(t => t.value === st.showTimeType)?.cls ?? 'st-block--normal',
      bufferTop,
      bufferHeight,
      bufferMinutes,
    };
  }

  // ── Week navigation ───────────────────────────────────────────────────────────
  // Changing the week changes the server-side range, so these reload rather than re-slice.
  prevWeek(): void { this.weekStart = this._addDays(this.weekStart, -7); this.load(); }
  nextWeek(): void { this.weekStart = this._addDays(this.weekStart, 7); this.load(); }
  goToday(): void { this.weekStart = this._mondayOf(new Date()); this.load(); }
  onFilterChange(): void { this._rebuild(); }

  get weekLabel(): string {
    const end = this._addDays(this.weekStart, 6);
    const fmt = (d: Date) => `${d.getDate()}/${d.getMonth() + 1}`;
    return `${fmt(this.weekStart)} – ${fmt(end)}/${end.getFullYear()}`;
  }

  // ── Create / edit ─────────────────────────────────────────────────────────────
  openCreate(): void {
    this._dialog.open(ShowTimeDialog, {
      width: '800px',
      data: { showTime: null, weekStart: this.weekStart, movies: this.movies, theaters: this.theaters, rooms: this.rooms },
    }).afterClosed().subscribe(changed => { if (changed) { this.load(); } });
  }

  edit(st: Dto): void {
    this._dialog.open(ShowTimeDialog, {
      width: '800px',
      data: { showTime: st, weekStart: this.weekStart, movies: this.movies, theaters: this.theaters, rooms: this.rooms },
    }).afterClosed().subscribe(changed => { if (changed) { this.load(); } });
  }

  // ── Labels ────────────────────────────────────────────────────────────────────
  movieTitle(id?: string): string { return this.movies.find(m => m.id === id)?.title ?? '—'; }
  formLabel(v?: CinemaServiceAgent.ProjectionForm): string { return this.projectionForms.find(x => x.value === v)?.name ?? '—'; }

  // ── Date helpers ──────────────────────────────────────────────────────────────
  private _pad(n: number): string { return `${n}`.padStart(2, '0'); }
  private _hm(d: Date): string { return `${this._pad(d.getHours())}:${this._pad(d.getMinutes())}`; }
  private _ymd(d: Date): string { return `${d.getFullYear()}-${this._pad(d.getMonth() + 1)}-${this._pad(d.getDate())}`; }
  private _hoursFromStart(d: Date): number { return (d.getHours() - this.startHour) + d.getMinutes() / 60; }
  /** Local-time ISO (no timezone suffix) so the server reads the same wall-clock week boundary we display. */
  private _isoLocal(d: Date): string { return `${this._ymd(d)}T${this._pad(d.getHours())}:${this._pad(d.getMinutes())}:00`; }
  private _addDays(d: Date, n: number): Date { const r = new Date(d); r.setDate(r.getDate() + n); return r; }
  private _sameDay(a: Date, b: Date): boolean {
    return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
  }
  private _mondayOf(d: Date): Date {
    const r = new Date(d.getFullYear(), d.getMonth(), d.getDate());
    const dow = (r.getDay() + 6) % 7; // 0 = Monday
    r.setDate(r.getDate() - dow);
    return r;
  }
  private _dow(d: Date): string {
    const days = ['CN', 'Th 2', 'Th 3', 'Th 4', 'Th 5', 'Th 6', 'Th 7'];
    return days[d.getDay()];
  }
}

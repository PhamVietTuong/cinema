import { ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Subject, merge, takeUntil } from 'rxjs';
import { CinemaServiceAgent, ProjectionFormValues, ShowTimeTypeValues, fetchAllPages } from 'CinemaLib';
import { ShowTimeDialog } from './show-time.dialog';

/** Slim calendar row; the full record is fetched with `getShowTime` only when a block is opened. */
type Dto = CinemaServiceAgent.ShowTimeCalendarItemDTO;

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
  private _reload$ = new Subject<void>();

  // ── Grid geometry ───────────────────────────────────────────────────────────
  readonly startHour = 8;          // first row label
  readonly endHour = 24;           // last row boundary (exclusive label at 24:00)
  readonly rowHeight = 60;         // px per hour
  readonly hours: number[] = Array.from({ length: this.endHour - this.startHour }, (_, i) => this.startHour + i);
  get gridHeight(): number { return (this.endHour - this.startHour) * this.rowHeight; }

  // ── Lookups ─────────────────────────────────────────────────────────────────
  movies: CinemaServiceAgent.MovieDTO[] = [];
  private _movieTitles = new Map<string, string>();
  rooms: CinemaServiceAgent.RoomDTO[] = [];
  theaters: CinemaServiceAgent.TheaterDTO[] = [];
  readonly showTimeTypes = ShowTimeTypeValues;
  readonly projectionForms = ProjectionFormValues;

  // ── Week state ────────────────────────────────────────────────────────────────
  weekStart!: Date;               // Monday 00:00 of the visible week
  days: DayColumn[] = [];
  filterMovieId = '';
  filterTheaterId = '';
  filterForm: CinemaServiceAgent.ProjectionForm | '' = '';

  private _showtimes: Dto[] = [];

  ngOnInit(): void {
    this.weekStart = this._mondayOf(new Date());
    const lookupPageSize = 200;

    fetchAllPages<CinemaServiceAgent.MovieDTO>(
      pageIndex => this._svc.getMovies(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex, pageSize: lookupPageSize })),
      lookupPageSize,
      m => m.id,
    ).pipe(takeUntil(this._destroy$))
      .subscribe(movies => {
        this.movies = movies;
        this._movieTitles = new Map(movies.filter(m => !!m.id).map(m => [m.id as string, m.title ?? ''] as const));
        this._rebuild(); // block titles come from the movie lookup, which can land after the showtimes
      });

    fetchAllPages<CinemaServiceAgent.TheaterDTO>(
      pageIndex => this._svc.getTheaters(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex, pageSize: lookupPageSize })),
      lookupPageSize,
      t => t.id,
    ).pipe(takeUntil(this._destroy$))
      .subscribe({
        next: theaters => {
          this.theaters = theaters;
          // A whole week across every cinema is thousands of overlapping blocks nobody can read, so open
          // on one cinema; "All cinemas" stays selectable. The first load waits for this lookup so the
          // page never fetches the unfiltered week first.
          this.filterTheaterId = theaters.find(t => !!t.id)?.id ?? '';
          this.load();
        },
        // Without the lookup there is nothing to default to; still show the week rather than a blank grid.
        error: () => this.load(),
      });

    fetchAllPages<CinemaServiceAgent.RoomDTO>(
      pageIndex => this._svc.getRooms(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex, pageSize: lookupPageSize })),
      lookupPageSize,
      r => r.id,
    ).pipe(takeUntil(this._destroy$))
      .subscribe(rooms => { this.rooms = rooms; this._cdr.markForCheck(); });
  }

  ngOnDestroy(): void {
    this._destroy$.next();
    this._destroy$.complete();
    this._reload$.complete();
  }

  // ── Data ────────────────────────────────────────────────────────────────────
  load(): void {
    // Filters run in SQL (movie, theater, format) and the endpoint returns slim rows, so a week is a
    // handful of big pages that fetchAllPages requests in parallel. A newer load cancels an older one.
    this._reload$.next();
    const pageSize = 200; // the API's PagingSearchDTO cap
    const filters: Record<string, string> = {
      from: this._isoLocal(this.weekStart),
      to: this._isoLocal(this._addDays(this.weekStart, 7)),
    };
    if (this.filterMovieId) { filters['movieId'] = this.filterMovieId; }
    if (this.filterTheaterId) { filters['theaterId'] = this.filterTheaterId; }
    if (this.filterForm !== '') { filters['projectionForm'] = String(this.filterForm); }

    fetchAllPages<Dto>(
      pageIndex => this._svc.getShowTimeCalendar(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex, pageSize, filters })),
      pageSize,
      st => st.id,
    )
      .pipe(takeUntil(merge(this._reload$, this._destroy$)))
      .subscribe(all => {
        this._showtimes = all;
        this._rebuild();
      });
  }

  private _rebuild(): void {
    const weekEnd = this._addDays(this.weekStart, 7);
    const today = new Date();
    this.days = Array.from({ length: 7 }, (_, i) => {
      const date = this._addDays(this.weekStart, i);
      return {
        date,
        dow: this._dow(date),
        dom: date.getDate(),
        isToday: this._sameDay(date, today),
        blocks: [] as PlacedBlock[],
      };
    });
    const dayByKey = new Map(this.days.map(d => [this._ymd(d.date), d]));

    for (const st of this._showtimes) {
      if (!st.startTime || !st.endTime) { continue; }
      const start = new Date(st.startTime);
      const end = new Date(st.endTime);
      if (start < this.weekStart || start >= weekEnd) { continue; }

      const col = dayByKey.get(this._ymd(start));
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
  // Filters are applied by the server, so a change reloads the week.
  onFilterChange(): void { this.load(); }

  get weekLabel(): string {
    const end = this._addDays(this.weekStart, 6);
    const fmt = (d: Date) => `${d.getDate()}/${d.getMonth() + 1}`;
    return `${fmt(this.weekStart)} – ${fmt(end)}/${end.getFullYear()}`;
  }

  // ── Create / edit ─────────────────────────────────────────────────────────────
  openCreate(): void {
    this._openDialog(null);
  }

  /** The grid only holds slim rows, so fetch the full record (room name, price…) before editing. */
  edit(st: Dto): void {
    this._svc.getShowTime(st.id).pipe(takeUntil(this._destroy$)).subscribe(full => this._openDialog(full));
  }

  private _openDialog(showTime: CinemaServiceAgent.ShowTimeDTO | null): void {
    this._dialog.open(ShowTimeDialog, {
      width: '800px',
      maxWidth: '100vw',
      data: { showTime, weekStart: this.weekStart, movies: this.movies, theaters: this.theaters, rooms: this.rooms },
    }).afterClosed().subscribe(changed => { if (changed) { this.load(); } });
  }

  // ── Labels ────────────────────────────────────────────────────────────────────
  movieTitle(id?: string): string { return (id ? this._movieTitles.get(id) : undefined) ?? '—'; }
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

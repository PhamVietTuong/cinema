import { ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';

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
  standalone: true,
  imports: [SharedModule],
  templateUrl: './show-times.component.html',
  styleUrl: './show-times.component.scss',
})
export class ShowTimesManagementComponent implements OnInit, OnDestroy {
  private _svc = inject(CinemaServiceAgent.HttpService);
  private _fb = inject(FormBuilder);
  private _cdr = inject(ChangeDetectorRef);
  private _destroy$ = new Subject<void>();

  // ── Grid geometry ───────────────────────────────────────────────────────────
  readonly startHour = 8;          // first row label
  readonly endHour = 24;           // last row boundary (exclusive label at 24:00)
  readonly rowHeight = 60;         // px per hour
  readonly hours: number[] = Array.from({ length: this.endHour - this.startHour }, (_, i) => this.startHour + i);
  get gridHeight(): number { return (this.endHour - this.startHour) * this.rowHeight; }

  // ── Lookups ─────────────────────────────────────────────────────────────────
  movies: CinemaServiceAgent.MovieDTO[] = [];
  readonly projectionForms = [
    { v: CinemaServiceAgent.ProjectionForm.TwoD, label: '2D' },
    { v: CinemaServiceAgent.ProjectionForm.ThreeD, label: '3D' },
    { v: CinemaServiceAgent.ProjectionForm.IMAX, label: 'IMAX' },
  ];
  readonly showTimeTypes = [
    { v: CinemaServiceAgent.ShowTimeType.Normal, label: 'Thường', cls: 'st-block--normal' },
    { v: CinemaServiceAgent.ShowTimeType.Premiere, label: 'Công Chiếu', cls: 'st-block--premiere' },
    { v: CinemaServiceAgent.ShowTimeType.Special, label: 'Đặc Biệt', cls: 'st-block--special' },
  ];

  // ── Week state ────────────────────────────────────────────────────────────────
  weekStart!: Date;               // Monday 00:00 of the visible week
  days: DayColumn[] = [];
  filterMovieId = '';

  private _showtimes: Dto[] = [];

  // ── Drawer / form ─────────────────────────────────────────────────────────────
  showForm = false;
  editingId: string | null = null;
  form: FormGroup = this._fb.group({
    movieId: ['', Validators.required],
    date: ['', Validators.required],
    start: ['', Validators.required],
    end: ['', Validators.required],
    projectionForm: [CinemaServiceAgent.ProjectionForm.TwoD, Validators.required],
    showTimeType: [CinemaServiceAgent.ShowTimeType.Normal, Validators.required],
    isActive: [true],
  });

  ngOnInit(): void {
    this.weekStart = this._mondayOf(new Date());
    this._svc.getMovies(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 500 }))
      .pipe(takeUntil(this._destroy$))
      .subscribe(r => { this.movies = r.results ?? []; this._cdr.markForCheck(); });
    this.load();
  }

  ngOnDestroy(): void {
    this._destroy$.next();
    this._destroy$.complete();
  }

  // ── Data ────────────────────────────────────────────────────────────────────
  load(): void {
    // The backend has no week-range filter for showtimes, so we fetch a wide page
    // and slice the visible week on the client.
    this._svc.getShowTimeList(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 1000 }))
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
    return {
      st,
      top,
      height: Math.max(28, rawHeight),
      title: this.movieTitle(st.movieId),
      timeLabel: `${this._hm(start)} – ${this._hm(end)}`,
      formLabel: this.formLabel(st.projectionForm),
      typeClass: this.showTimeTypes.find(t => t.v === st.showTimeType)?.cls ?? 'st-block--normal',
    };
  }

  // ── Week navigation ───────────────────────────────────────────────────────────
  prevWeek(): void { this.weekStart = this._addDays(this.weekStart, -7); this._rebuild(); }
  nextWeek(): void { this.weekStart = this._addDays(this.weekStart, 7); this._rebuild(); }
  goToday(): void { this.weekStart = this._mondayOf(new Date()); this._rebuild(); }
  onFilterChange(): void { this._rebuild(); }

  get weekLabel(): string {
    const end = this._addDays(this.weekStart, 6);
    const fmt = (d: Date) => `${d.getDate()}/${d.getMonth() + 1}`;
    return `${fmt(this.weekStart)} – ${fmt(end)}/${end.getFullYear()}`;
  }

  // ── Create / edit / delete ──────────────────────────────────────────────────
  openCreate(): void {
    this.editingId = null;
    const anchor = this._isThisWeek(new Date()) ? new Date() : this.weekStart;
    this.form.reset({
      movieId: '',
      date: this._ymd(anchor),
      start: '',
      end: '',
      projectionForm: CinemaServiceAgent.ProjectionForm.TwoD,
      showTimeType: CinemaServiceAgent.ShowTimeType.Normal,
      isActive: true,
    });
    this.showForm = true;
  }

  edit(st: Dto): void {
    if (!st.startTime || !st.endTime) { return; }
    const start = new Date(st.startTime);
    const end = new Date(st.endTime);
    this.editingId = st.id ?? null;
    this.form.reset({
      movieId: st.movieId ?? '',
      date: this._ymd(start),
      start: this._hm(start),
      end: this._hm(end),
      projectionForm: st.projectionForm ?? CinemaServiceAgent.ProjectionForm.TwoD,
      showTimeType: st.showTimeType ?? CinemaServiceAgent.ShowTimeType.Normal,
      isActive: st.isActive ?? true,
    });
    this.showForm = true;
  }

  save(): void {
    if (!this.form.valid) { this.form.markAllAsTouched(); return; }
    const v = this.form.value;
    const payload = {
      movieId: v.movieId,
      startTime: `${v.date}T${v.start}`,
      endTime: `${v.date}T${v.end}`,
      projectionForm: v.projectionForm,
      showTimeType: v.showTimeType,
      isActive: v.isActive,
    };
    const obs = this.editingId
      ? this._svc.updateShowTime(CinemaServiceAgent.UpdateShowTimeRequest.fromJS({ ...payload, id: this.editingId }))
      : this._svc.createShowTime(CinemaServiceAgent.CreateShowTimeRequest.fromJS(payload));
    obs.pipe(takeUntil(this._destroy$)).subscribe(() => { this.cancel(); this.load(); });
  }

  deleteCurrent(): void {
    const id = this.editingId;
    if (id && confirm('Bạn có chắc muốn xóa suất chiếu này?')) {
      this._svc.deleteShowTime(id).pipe(takeUntil(this._destroy$)).subscribe(() => { this.cancel(); this.load(); });
    }
  }

  cancel(): void {
    this.showForm = false;
    this.editingId = null;
  }

  // ── Labels ────────────────────────────────────────────────────────────────────
  movieTitle(id?: string): string { return this.movies.find(m => m.id === id)?.title ?? '—'; }
  moviePoster(id?: string): string | undefined { return this.movies.find(m => m.id === id)?.posterUrl; }
  formLabel(v?: CinemaServiceAgent.ProjectionForm): string { return this.projectionForms.find(x => x.v === v)?.label ?? '—'; }

  // ── Date helpers ──────────────────────────────────────────────────────────────
  private _pad(n: number): string { return `${n}`.padStart(2, '0'); }
  private _hm(d: Date): string { return `${this._pad(d.getHours())}:${this._pad(d.getMinutes())}`; }
  private _ymd(d: Date): string { return `${d.getFullYear()}-${this._pad(d.getMonth() + 1)}-${this._pad(d.getDate())}`; }
  private _hoursFromStart(d: Date): number { return (d.getHours() - this.startHour) + d.getMinutes() / 60; }
  private _addDays(d: Date, n: number): Date { const r = new Date(d); r.setDate(r.getDate() + n); return r; }
  private _sameDay(a: Date, b: Date): boolean {
    return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
  }
  private _isThisWeek(d: Date): boolean {
    return d >= this.weekStart && d < this._addDays(this.weekStart, 7);
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

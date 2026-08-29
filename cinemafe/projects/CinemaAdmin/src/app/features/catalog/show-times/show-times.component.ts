import { ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { AbstractControl, FormBuilder, FormGroup, ValidationErrors, Validators } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { SharedModule, CinemaServiceAgent, ProjectionFormValues, ShowTimeTypeValues, apiErrorMessage } from 'CinemaLib';
import { ConfirmModalComponent } from '../../../shared/confirm-modal.component';
import { ModalComponent } from '../../../shared/modal.component';

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
  imports: [SharedModule, ConfirmModalComponent, ModalComponent],
  templateUrl: './show-times.component.html',
  styleUrl: './show-times.component.scss',
})
export class ShowTimesManagementComponent implements OnInit, OnDestroy {
  private _svc = inject(CinemaServiceAgent.HttpService);
  private _fb = inject(FormBuilder);
  private _cdr = inject(ChangeDetectorRef);
  private _translate = inject(TranslateService);
  private _destroy$ = new Subject<void>();

  confirmOpen = false;

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
  readonly projectionForms = ProjectionFormValues;
  readonly showTimeTypes = ShowTimeTypeValues;

  // ── Week state ────────────────────────────────────────────────────────────────
  weekStart!: Date;               // Monday 00:00 of the visible week
  days: DayColumn[] = [];
  filterMovieId = '';

  private _showtimes: Dto[] = [];

  // ── Dialog / form ───────────────────────────────────────────────────────────
  showForm = false;
  editingId: string | null = null;

  /**
   * Why the API rejected the last save/delete. The error interceptor deliberately lets 400/404
   * through untouched so the component can show them where the user can act on them — without
   * this the request just failed silently and the dialog sat there looking idle.
   */
  formError: string | null = null;

  /** Today as yyyy-MM-dd — the earliest date a new showtime may be scheduled on. */
  get todayYmd(): string { return this._ymd(new Date()); }

  /**
   * Blocks scheduling a new showtime in the past. Editing stays unrestricted: an admin may be
   * correcting the record of a screening that already ran, which is also what the API allows
   * (ShowTimeManager only passes mustBeFuture on create).
   */
  private _notPastOnCreate = (control: AbstractControl): ValidationErrors | null => {
    if (this.editingId || !control.value) { return null; }
    return control.value < this.todayYmd ? { pastDate: true } : null;
  };

  form: FormGroup = this._fb.group({
    movieId: ['', Validators.required],
    date: ['', [Validators.required, this._notPastOnCreate]],
    start: ['', Validators.required],
    end: ['', Validators.required],
    projectionForm: [CinemaServiceAgent.ProjectionForm.TwoD, Validators.required],
    showTimeType: [CinemaServiceAgent.ShowTimeType.Normal, Validators.required],
    theaterId: ['', Validators.required],
    roomId: ['', Validators.required],
    basePrice: [75000, [Validators.required, Validators.min(0)]],
    isActive: [true],
  });

  /** Rooms of the picked theater. Empty until one is picked, so the two selects cascade. */
  get roomsForTheater(): CinemaServiceAgent.RoomDTO[] {
    const theaterId = this.form.value.theaterId;
    return theaterId ? this.rooms.filter(r => r.theaterId === theaterId) : [];
  }

  /** A room belongs to exactly one theater, so switching theater invalidates the picked room. */
  onTheaterChange(): void {
    const roomId = this.form.value.roomId;
    if (roomId && !this.roomsForTheater.some(r => r.id === roomId)) {
      this.form.patchValue({ roomId: '' });
    }
  }

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
    return {
      st,
      top,
      height: Math.max(28, rawHeight),
      title: this.movieTitle(st.movieId),
      timeLabel: `${this._hm(start)} – ${this._hm(end)}`,
      formLabel: this.formLabel(st.projectionForm),
      typeClass: this.showTimeTypes.find(t => t.value === st.showTimeType)?.cls ?? 'st-block--normal',
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

  // ── Create / edit / delete ──────────────────────────────────────────────────
  openCreate(): void {
    this.editingId = null;
    // Anchor on the visible week, but never before today — a new showtime cannot be scheduled
    // in the past, so pre-filling a past date from a back-navigated week would open the dialog
    // already invalid.
    const today = new Date();
    const anchor = this._isThisWeek(today) || this.weekStart < today ? today : this.weekStart;
    this.form.reset({
      movieId: '',
      date: this._ymd(anchor),
      start: '',
      end: '',
      projectionForm: CinemaServiceAgent.ProjectionForm.TwoD,
      showTimeType: CinemaServiceAgent.ShowTimeType.Normal,
      theaterId: '',
      roomId: '',
      basePrice: 75000,
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
      // A showtime stores only its room; the theater is implied by it and drives the cascade.
      theaterId: this.rooms.find(r => r.id === st.roomId)?.theaterId ?? '',
      roomId: st.roomId ?? '',
      basePrice: st.basePrice ?? 75000,
      isActive: st.isActive ?? true,
    });
    this.showForm = true;
  }

  save(): void {
    if (!this.form.valid) { this.form.markAllAsTouched(); return; }
    const v = this.form.value;
    const payload = {
      movieId: v.movieId,
      // Append 'Z' so the picked wall-clock time is preserved end-to-end: the generated
      // DTO serialises Date via toISOString(), so without this the value is shifted by the
      // browser's UTC offset on save and again on read (e.g. 16:00 → 09:00).
      startTime: `${v.date}T${v.start}:00Z`,
      endTime: `${v.date}T${v.end}:00Z`,
      projectionForm: v.projectionForm,
      showTimeType: v.showTimeType,
      // theaterId is a UI-only cascade field; the API derives the theater from the room.
      roomId: v.roomId,
      basePrice: Number(v.basePrice),
      isActive: v.isActive,
    };
    const obs = this.editingId
      ? this._svc.updateShowTime(CinemaServiceAgent.UpdateShowTimeRequest.fromJS({ ...payload, id: this.editingId }))
      : this._svc.createShowTime(CinemaServiceAgent.CreateShowTimeRequest.fromJS(payload));
    this.formError = null;
    obs.pipe(takeUntil(this._destroy$)).subscribe({
      next: () => { this.cancel(); this.load(); },
      error: e => { this._showError(e, 'showTimes.saveFailed'); },
    });
  }

  deleteCurrent(): void {
    if (!this.editingId) { return; }
    this.confirmOpen = true;
  }

  confirmDelete(): void {
    const id = this.editingId;
    this.confirmOpen = false;
    if (id) {
      this.formError = null;
      this._svc.deleteShowTime(id).pipe(takeUntil(this._destroy$)).subscribe({
        next: () => { this.cancel(); this.load(); },
        error: e => { this._showError(e, 'showTimes.deleteFailed'); },
      });
    }
  }

  cancel(): void {
    this.showForm = false;
    this.editingId = null;
    this.formError = null;
  }

  /** Zoneless app: nothing re-renders off an rxjs error callback without markForCheck. */
  private _showError(err: unknown, fallbackKey: string): void {
    this.formError = apiErrorMessage(err, this._translate.instant(fallbackKey));
    this._cdr.markForCheck();
  }

  // ── Labels ────────────────────────────────────────────────────────────────────
  movieTitle(id?: string): string { return this.movies.find(m => m.id === id)?.title ?? '—'; }
  moviePoster(id?: string): string | undefined { return this.movies.find(m => m.id === id)?.posterUrl; }
  formLabel(v?: CinemaServiceAgent.ProjectionForm): string { return this.projectionForms.find(x => x.value === v)?.name ?? '—'; }
  theaterName(id?: string): string { return this.theaters.find(t => t.id === id)?.name ?? '—'; }
  roomLabel(r: CinemaServiceAgent.RoomDTO): string { return `${this.theaterName(r.theaterId)} · ${r.name}`; }

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

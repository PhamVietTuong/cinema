import { ChangeDetectorRef, Component, HostListener, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, CanDeactivateFn, Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { EMPTY, Subject, Subscription } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, startWith, switchMap, tap } from 'rxjs/operators';
import { CinemaServiceAgent, CreditTypeValues, showLoading, hideLoading, showSuccess, showError, showException } from 'CinemaLib';
import { ImageUploadService } from '../../../../shared/image-upload.service';

/** One credit line on the page. `personId` is null for a person created here and not saved yet. */
export interface CastRow {
  key: number;
  personId: string | null;
  name: string;
  photoUrl: string;
  creditType: CinemaServiceAgent.CreditType;
  characterName: string;
  expanded: boolean;
  uploading: boolean;
  uploadError: boolean;
}

interface CastGroup {
  type: CinemaServiceAgent.CreditType;
  labelKey: string;
}

/** Route guard: prompts before leaving with unsaved edits. */
export const movieCastCanDeactivate: CanDeactivateFn<MovieCastComponent> = component => component.confirmLeave();

/** Dedicated page for a movie's Cast & Crew: grouped rows edited in place, plus a library of
 * existing people to add from. Saves through updateMovie (credits are part of that request). */
@Component({
  selector: 'app-movie-cast',
  standalone: false,
  templateUrl: './movie-cast.component.html',
  styleUrl: './movie-cast.component.scss',
})
export class MovieCastComponent implements OnInit, OnDestroy {
  readonly CreditType = CinemaServiceAgent.CreditType;
  readonly groups: CastGroup[] = [
    { type: CinemaServiceAgent.CreditType.Director, labelKey: 'movies.cast.groupDirector' },
    { type: CinemaServiceAgent.CreditType.Actor, labelKey: 'movies.cast.groupActor' },
  ];
  readonly creditTypeOptions = CreditTypeValues;

  movie: CinemaServiceAgent.MovieDetailDTO | null = null;
  rows: CastRow[] = [];
  loading = true;
  loadFailed = false;
  saving = false;

  /** JSON of the saved state; the page is "dirty" whenever the current rows serialize differently. */
  private _baseline = '[]';
  private _nextKey = 1;

  library: CinemaServiceAgent.PersonDTO[] = [];
  libraryLoading = false;
  libraryTotal = 0;
  private readonly _libraryPageSize = 100;
  private readonly _query$ = new Subject<string>();
  private _querySub?: Subscription;
  private _query = '';

  creating = false;
  newName = '';
  newPhotoUrl = '';
  newType: CinemaServiceAgent.CreditType = CinemaServiceAgent.CreditType.Actor;
  newUploading = false;
  newUploadError = false;
  private _newUploadToken = 0;

  constructor(
    private _route: ActivatedRoute,
    private _router: Router,
    private _svc: CinemaServiceAgent.HttpService,
    private _store: Store<any>,
    private _upload: ImageUploadService,
    private _cdr: ChangeDetectorRef,
    private _i18n: TranslateService,
  ) {}

  ngOnInit(): void {
    this._loadMovie();
    this._querySub = this._query$.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      startWith(this._query),
      tap(q => {
        this._query = q;
        this.libraryLoading = true;
        this._cdr.markForCheck();
      }),
      switchMap(() => this._fetchLibrary()),
    ).subscribe(r => {
      this.library = r?.results ?? [];
      this.libraryTotal = r?.totalCount ?? this.library.length;
      this.libraryLoading = false;
      this._cdr.markForCheck();
    });
  }

  private _loadMovie(): void {
    const id = this._route.snapshot.paramMap.get('id');
    if (!id) {
      this.loadFailed = true;
      this.loading = false;
      return;
    }
    this._svc.getMovie(id).subscribe({
      next: movie => {
        this.movie = movie;
        this.rows = (movie.credits ?? []).map(c => this._newRow({
          personId: c.personId ?? null,
          name: c.personName ?? '',
          photoUrl: c.personPhotoUrl ?? '',
          creditType: c.creditType ?? CinemaServiceAgent.CreditType.Actor,
          characterName: c.characterName ?? '',
        }));
        this._baseline = this._snapshot();
        this.loading = false;
        this._cdr.markForCheck();
      },
      error: () => {
        this.loadFailed = true;
        this.loading = false;
        this._cdr.markForCheck();
      },
    });
  }

  ngOnDestroy(): void {
    this._querySub?.unsubscribe();
  }

  // ── State helpers ──────────────────────────────────────────────────────────

  private _newRow(init: Pick<CastRow, 'personId' | 'name' | 'photoUrl' | 'creditType' | 'characterName'>): CastRow {
    return { ...init, key: this._nextKey++, expanded: false, uploading: false, uploadError: false };
  }

  private _snapshot(): string {
    return JSON.stringify(this._orderedRows().map(r => [r.personId, r.name.trim(), r.photoUrl.trim(), r.creditType, r.creditType === CinemaServiceAgent.CreditType.Actor ? r.characterName.trim() : '']));
  }

  /** Rows in saved order: groups first (Director, then Actor), array order inside each group. */
  private _orderedRows(): CastRow[] {
    return this.groups.flatMap(g => this.rowsOf(g.type));
  }

  rowsOf(type: CinemaServiceAgent.CreditType): CastRow[] {
    return this.rows.filter(r => r.creditType === type);
  }

  get dirty(): boolean {
    return this._snapshot() !== this._baseline;
  }

  get valid(): boolean {
    return this.rows.every(r => r.name.trim().length > 0 && r.name.length <= 200 && r.photoUrl.length <= 500 && r.characterName.length <= 200);
  }

  get canSave(): boolean {
    return !!this.movie && !this.saving && this.dirty && this.valid && !this.newUploading && !this.rows.some(r => r.uploading);
  }

  isDirtyRow(row: CastRow): boolean {
    const base = JSON.parse(this._baseline) as unknown[][];
    const mine = JSON.stringify([row.personId, row.name.trim(), row.photoUrl.trim(), row.creditType, row.creditType === CinemaServiceAgent.CreditType.Actor ? row.characterName.trim() : '']);
    return row.personId === null || !base.some(b => JSON.stringify(b) === mine);
  }

  /** Added/removed rows versus the baseline (multiset diff); order-only changes count as 1. */
  get changeCount(): number {
    const base = (JSON.parse(this._baseline) as unknown[][]).map(b => JSON.stringify(b));
    const now = (JSON.parse(this._snapshot()) as unknown[][]).map(b => JSON.stringify(b));
    const remaining = [...base];
    let added = 0;
    for (const n of now) {
      const idx = remaining.indexOf(n);
      if (idx < 0) {
        added++;
      } else {
        remaining.splice(idx, 1);
      }
    }
    const n = Math.max(added, remaining.length);
    return n === 0 && this.dirty ? 1 : n;
  }

  /** Already credited (in any role) — shown as "added" in the library. */
  isInMovie(personId?: string): boolean {
    return !!personId && this.rows.some(r => r.personId === personId);
  }

  // ── Row actions ────────────────────────────────────────────────────────────

  move(row: CastRow, delta: -1 | 1): void {
    const peers = this.rowsOf(row.creditType);
    const target = peers[peers.indexOf(row) + delta];
    if (!target) {
      return;
    }
    const a = this.rows.indexOf(row);
    const b = this.rows.indexOf(target);
    [this.rows[a], this.rows[b]] = [this.rows[b], this.rows[a]];
    this.rows = [...this.rows];
  }

  isFirst(row: CastRow): boolean {
    return this.rowsOf(row.creditType)[0] === row;
  }

  isLast(row: CastRow): boolean {
    const peers = this.rowsOf(row.creditType);
    return peers[peers.length - 1] === row;
  }

  remove(row: CastRow): void {
    this.rows = this.rows.filter(r => r !== row);
  }

  /** Switch role. The backend rejects two rows with the same (person, role), so a clash is refused. */
  setType(row: CastRow, type: CinemaServiceAgent.CreditType): void {
    if (row.creditType === type) {
      return;
    }
    if (row.personId && this.rows.some(r => r !== row && r.personId === row.personId && r.creditType === type)) {
      this._store.dispatch(showError({ message: this._i18n.instant('movies.cast.alreadyHasRole') }));
      return;
    }
    row.creditType = type;
    this.rows = [...this.rows];
  }

  /** Name/photo belong to the person, so every row for the same person (Director + Actor) must agree. */
  onPersonEdited(row: CastRow, field: 'name' | 'photoUrl'): void {
    if (!row.personId) {
      return;
    }
    for (const r of this.rows) {
      if (r !== row && r.personId === row.personId) {
        r[field] = row[field];
      }
    }
  }

  onPickRowPhoto(event: Event, row: CastRow): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
    row.uploading = true;
    row.uploadError = false;
    this._upload.upload(file).subscribe({
      next: url => {
        row.uploading = false;
        if (this.rows.includes(row)) {
          row.photoUrl = url;
          this.onPersonEdited(row, 'photoUrl');
        }
        this._cdr.markForCheck();
      },
      error: () => {
        row.uploading = false;
        if (this.rows.includes(row)) {
          row.uploadError = true;
        }
        this._cdr.markForCheck();
      },
    });
  }

  // ── Library ────────────────────────────────────────────────────────────────

  onSearch(value: string): void {
    this._query$.next(value.trim());
  }

  private _fetchLibrary() {
    const filters: Record<string, string> = {};
    if (this._query) {
      filters['keyword'] = this._query;
    }
    return this._svc.getPeople(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: this._libraryPageSize, filters })).pipe(
      catchError(() => {
        this.libraryLoading = false;
        this._cdr.markForCheck();
        return EMPTY;
      }),
    );
  }

  addFromLibrary(person: CinemaServiceAgent.PersonDTO, type = CinemaServiceAgent.CreditType.Actor): void {
    if (!person.id) {
      return;
    }
    if (this.rows.some(r => r.personId === person.id && r.creditType === type)) {
      this._store.dispatch(showError({ message: this._i18n.instant('movies.cast.alreadyHasRole') }));
      return;
    }
    // Reuse the name/photo another row already carries so unsaved edits stay consistent.
    const sibling = this.rows.find(r => r.personId === person.id);
    this.rows = [...this.rows, this._newRow({
      personId: person.id,
      name: sibling?.name ?? person.name ?? '',
      photoUrl: sibling?.photoUrl ?? person.photoUrl ?? '',
      creditType: type,
      characterName: '',
    })];
  }

  // ── New person ─────────────────────────────────────────────────────────────

  openCreate(): void {
    this.creating = true;
  }

  cancelCreate(): void {
    this.creating = false;
    this.newName = '';
    this.newPhotoUrl = '';
    this.newType = CinemaServiceAgent.CreditType.Actor;
    this.newUploadError = false;
    this.newUploading = false;
    this._newUploadToken++;
  }

  onPickNewPhoto(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
    const token = ++this._newUploadToken;
    this.newUploading = true;
    this.newUploadError = false;
    this._upload.upload(file).subscribe({
      next: url => {
        if (token === this._newUploadToken) {
          this.newPhotoUrl = url;
          this.newUploading = false;
          this._cdr.markForCheck();
        }
      },
      error: () => {
        if (token === this._newUploadToken) {
          this.newUploading = false;
          this.newUploadError = true;
          this._cdr.markForCheck();
        }
      },
    });
  }

  confirmCreate(): void {
    const name = this.newName.trim();
    if (!name || this.newUploading) {
      return;
    }
    const lower = name.toLowerCase();
    if (this.rows.some(r => r.creditType === this.newType && r.name.trim().toLowerCase() === lower)) {
      this._store.dispatch(showError({ message: this._i18n.instant('movies.cast.alreadyHasRole') }));
      return;
    }
    this.rows = [...this.rows, this._newRow({
      personId: null, name, photoUrl: this.newPhotoUrl.trim(), creditType: this.newType, characterName: '',
    })];
    this.cancelCreate();
  }

  // ── Save / leave ───────────────────────────────────────────────────────────

  save(): void {
    const m = this.movie;
    if (!m || !this.canSave) {
      return;
    }
    const credits = this._orderedRows().map((r, i) => ({
      personId: r.personId ?? undefined,
      personName: r.name.trim(),
      personPhotoUrl: r.photoUrl.trim() || undefined,
      creditType: r.creditType,
      characterName: r.creditType === CinemaServiceAgent.CreditType.Actor ? (r.characterName.trim() || undefined) : undefined,
      displayOrder: i,
    }));
    const request = CinemaServiceAgent.UpdateMovieRequest.fromJS({
      id: m.id,
      title: m.title,
      description: m.description,
      duration: m.duration,
      releaseDate: m.releaseDate,
      endDate: m.endDate,
      posterUrl: m.posterUrl,
      trailerUrl: m.trailerUrl,
      director: m.director,
      cast: m.cast,
      language: m.language,
      subtitle: m.subtitle,
      imdbId: m.imdbId,
      ageRestrictionId: m.ageRestrictionId,
      movieTypeIds: m.movieTypeIds ?? [],
      credits,
    });
    this.saving = true;
    this._store.dispatch(showLoading());
    this._svc.updateMovie(request).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({}));
        this._reload();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this.saving = false;
      this._store.dispatch(hideLoading());
      this._cdr.markForCheck();
    });
  }

  /** Re-fetch so new people get their real ids (and names/photos reflect what the server kept). */
  private _reload(): void {
    this.loading = true;
    this.loadFailed = false;
    this._loadMovie();
  }

  discard(): void {
    this.rows = (JSON.parse(this._baseline) as [string | null, string, string, CinemaServiceAgent.CreditType, string][])
      .map(b => this._newRow({ personId: b[0], name: b[1], photoUrl: b[2], creditType: b[3], characterName: b[4] }));
  }

  back(): void {
    void this._router.navigate(['/movies']);
  }

  /** Used by the route guard: true when there is nothing to lose or the admin confirms. */
  confirmLeave(): boolean {
    return !this.dirty || window.confirm(this._i18n.instant('movies.cast.leaveConfirm'));
  }

  @HostListener('window:beforeunload', ['$event'])
  onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.dirty) {
      event.preventDefault();
    }
  }

  // ── Presentation ───────────────────────────────────────────────────────────

  initials(name: string): string {
    return name.split(/\s+/).filter(Boolean).slice(-2).map(s => s[0]).join('').toUpperCase();
  }

  hue(name: string): number {
    let h = 0;
    for (const ch of name) {
      h = (h * 31 + ch.charCodeAt(0)) % 360;
    }
    return h;
  }
}

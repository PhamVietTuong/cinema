import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, EventEmitter, HostListener, Input, Output, ViewChild, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl } from '@angular/forms';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { catchError, debounceTime, distinctUntilChanged, map, switchMap, tap } from 'rxjs/operators';
import { DatePipe } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslatePipe } from '@ngx-translate/core';
import { CinemaServiceAgent } from 'CinemaLib';
import { movieDetailCommands } from '../movie-route';

/** Autocomplete suggestions shown under the input. */
const SUGGESTION_COUNT = 6;
const MIN_QUERY_LENGTH = 2;
const DEBOUNCE_MS = 250;

let nextId = 0;

/**
 * Movie search with debounced autocomplete. `bar` is the desktop topbar form (an icon that opens a
 * full-width overlay over the topbar); `drawer` is the always-open variant inside the mobile menu.
 * Enter on a highlighted suggestion opens that movie, otherwise it opens /movies?q=<query>.
 * Arrow keys move through the suggestions, Escape closes.
 */
@Component({
  selector: 'app-search-box',
  standalone: true,
  // Deliberately not SharedModule: this is in the root shell, whose initial bundle is budgeted.
  imports: [ReactiveFormsModule, MatIconModule, MatProgressSpinnerModule, TranslatePipe, DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './search-box.component.html',
  styleUrl: './search-box.component.scss',
})
export class SearchBoxComponent {
  private readonly _api = inject(CinemaServiceAgent.HttpService);
  private readonly _router = inject(Router);
  private readonly _host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly _destroyRef = inject(DestroyRef);

  @Input() variant: 'bar' | 'drawer' = 'bar';
  /** Fired after navigating, so the mobile drawer can close itself. */
  @Output() navigated = new EventEmitter<void>();

  @ViewChild('input') private _input?: ElementRef<HTMLInputElement>;
  @ViewChild('toggle') private _toggle?: ElementRef<HTMLButtonElement>;

  readonly uid = `sb-${nextId++}`;
  readonly query = new FormControl('', { nonNullable: true });
  /** Bar variant: whether the overlay is expanded. The drawer variant is always expanded. */
  readonly expanded = signal(false);
  readonly results = signal<CinemaServiceAgent.MovieDTO[]>([]);
  readonly loading = signal(false);
  /** True once a search for the current text finished (so "no results" is not shown while typing). */
  readonly searched = signal(false);
  /** Highlighted row: 0..results-1 are movies, results.length is the "see all" row; -1 = none. */
  readonly active = signal(-1);
  readonly term = signal('');

  readonly showPanel = computed(() => this.term().length >= MIN_QUERY_LENGTH && (this.loading() || this.searched()));
  readonly optionCount = computed(() => (this.results().length > 0 ? this.results().length + 1 : 0));
  readonly activeId = computed(() => (this.active() >= 0 ? `${this.uid}-opt-${this.active()}` : null));

  constructor() {
    this.query.valueChanges.pipe(
      tap(v => {
        this.term.set(v.trim());
        this.active.set(-1);
        this.searched.set(false);
      }),
      debounceTime(DEBOUNCE_MS),
      map(v => v.trim()),
      distinctUntilChanged(),
      switchMap(q => {
        if (q.length < MIN_QUERY_LENGTH) {
          this.loading.set(false);
          this.results.set([]);
          return of(null);
        }
        this.loading.set(true);
        return this._api.getMovies(CinemaServiceAgent.PagingSearchDTO.fromJS({
          pageIndex: 1, pageSize: SUGGESTION_COUNT, filters: { search: q },
        })).pipe(
          map(r => ({ q, items: r.results ?? [] })),
          catchError(() => of({ q, items: [] as CinemaServiceAgent.MovieDTO[] })),
        );
      }),
      takeUntilDestroyed(this._destroyRef),
    ).subscribe(res => {
      if (!res) {
        return;
      }
      // Ignore a response that no longer matches what is typed (the user kept typing).
      if (res.q === this.term()) {
        this.results.set(res.items);
        this.loading.set(false);
        this.searched.set(true);
      }
    });
  }

  get isOpen(): boolean {
    return this.variant === 'drawer' || this.expanded();
  }

  open(): void {
    this.expanded.set(true);
    // The input only exists after the next render.
    setTimeout(() => this._input?.nativeElement.focus());
  }

  /** Collapses the bar overlay and clears the suggestions. */
  close(restoreFocus = true): void {
    this.expanded.set(false);
    this.active.set(-1);
    this.results.set([]);
    this.searched.set(false);
    this.loading.set(false);
    this.term.set('');
    this.query.setValue('', { emitEvent: false });
    if (restoreFocus) {
      setTimeout(() => this._toggle?.nativeElement.focus());
    }
  }

  clear(): void {
    this.query.setValue('');
    this._input?.nativeElement.focus();
  }

  @HostListener('document:pointerdown', ['$event'])
  onDocumentPointerDown(e: Event): void {
    if (this.variant === 'bar' && this.expanded() && !this._host.nativeElement.contains(e.target as Node)) {
      this.close(false);
    }
  }

  onKeydown(e: KeyboardEvent): void {
    const count = this.optionCount();
    switch (e.key) {
      case 'ArrowDown':
        if (count > 0) {
          this.active.set((this.active() + 1) % count);
          e.preventDefault();
        }
        break;
      case 'ArrowUp':
        if (count > 0) {
          this.active.set(this.active() <= 0 ? count - 1 : this.active() - 1);
          e.preventDefault();
        }
        break;
      case 'Enter':
        e.preventDefault();
        this.submit();
        break;
      case 'Escape':
        if (this.variant === 'bar') {
          this.close();
        } else {
          this.query.setValue('');
        }
        e.preventDefault();
        break;
      default:
        break;
    }
  }

  /** Enter / "see all": open the highlighted movie, or the movies page filtered by the query. */
  submit(): void {
    const idx = this.active();
    const movie = idx >= 0 ? this.results()[idx] : undefined;
    if (movie?.slug) {
      this.openMovie(movie);
      return;
    }
    const q = this.term();
    if (!q) {
      return;
    }
    void this._router.navigate(['/movies'], { queryParams: { q } });
    this._afterNavigate();
  }

  openMovie(movie: CinemaServiceAgent.MovieDTO): void {
    void this._router.navigate(movieDetailCommands(movie.slug ?? ''));
    this._afterNavigate();
  }

  hover(index: number): void {
    this.active.set(index);
  }

  onImgError(e: Event): void {
    const img = e.target as HTMLImageElement;
    if (!img.src.endsWith('assets/no-poster.jpg')) {
      img.src = 'assets/no-poster.jpg';
    }
  }

  private _afterNavigate(): void {
    if (this.variant === 'bar') {
      this.close(false);
    } else {
      this.query.setValue('', { emitEvent: false });
      this.term.set('');
      this.results.set([]);
      this.searched.set(false);
      this.active.set(-1);
    }
    this.navigated.emit();
  }
}

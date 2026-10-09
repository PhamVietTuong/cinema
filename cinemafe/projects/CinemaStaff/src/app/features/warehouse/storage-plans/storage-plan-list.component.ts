import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { Subject } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import {
  CinemaServiceAgent,
  EmptyStateComponent,
  FilterBarComponent,
  FilterBarField,
  SharedModule,
  StatusPillComponent,
  StoragePlanStatusValues,
  showException,
  hideLoading,
  showLoading,
} from 'CinemaLib';
import { TheaterContextService } from '../../../core/theater-context.service';

/** Paged list of the current theater's storage (restock) plans with status / code filters. */
@Component({
  selector: 'staff-storage-plan-list',
  standalone: true,
  imports: [SharedModule, StatusPillComponent, EmptyStateComponent, FilterBarComponent],
  templateUrl: './storage-plan-list.component.html',
  styleUrl: './storage-plan-list.component.scss',
})
export class StoragePlanListComponent implements OnInit {
  readonly pageSize = 10;

  /** Filter controls rendered by the shared filter bar. */
  readonly filterFields: FilterBarField[] = [
    { key: 'keyword', type: 'text', labelKey: 'storagePlans.list.code' },
    {
      key: 'status',
      type: 'select',
      labelKey: 'common.status',
      options: StoragePlanStatusValues.map(s => ({ value: String(s.value), labelKey: s.name })),
    },
  ];

  searchForm: FormGroup;
  rows: CinemaServiceAgent.StoragePlanListItemDTO[] = [];
  total = 0;
  pageIndex = 0; // 0-based for the paginator
  loading = false;

  private readonly _theaterContext = inject(TheaterContextService);
  private readonly _filterChange$ = new Subject<void>();

  constructor(
    private _cinema: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _router: Router,
    private _store: Store<any>,
    private _cd: ChangeDetectorRef,
  ) {
    this.searchForm = this._fb.group({ keyword: [''], status: [''] });

    // Reload when an Admin switches the topbar theater.
    effect(() => {
      this._theaterContext.currentTheaterId();
      this.pageIndex = 0;
      this.load();
    });
  }

  /** Theater the list is scoped to; empty while an Admin has not picked one. */
  get theaterId(): string {
    return this._theaterContext.currentTheaterId() ?? '';
  }

  ngOnInit(): void {
    this._filterChange$.pipe(debounceTime(300)).subscribe(() => {
      this.pageIndex = 0;
      this.load();
    });
  }

  onFilterChange(): void {
    this._filterChange$.next();
  }

  onPage(pageIndex: number): void {
    this.pageIndex = pageIndex;
    this.load();
  }

  load(): void {
    const theaterId = this.theaterId;
    if (!theaterId) {
      this.rows = [];
      this.total = 0;
      this._cd.markForCheck();
      return;
    }
    const filters: { [key: string]: string } = { theaterId };
    const raw = this.searchForm.value as Record<string, string>;
    for (const key of Object.keys(raw)) {
      const value = (raw[key] ?? '').toString().trim();
      if (value) {
        filters[key] = value;
      }
    }
    this.loading = true;
    this._store.dispatch(showLoading());
    this._cinema.getStoragePlans(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: this.pageIndex + 1, pageSize: this.pageSize, filters,
    })).subscribe({
      next: r => {
        this.rows = r.results ?? [];
        this.total = r.totalCount ?? 0;
        this._cd.markForCheck();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this.loading = false;
      this._store.dispatch(hideLoading());
      this._cd.markForCheck();
    });
  }

  openNew(): void {
    this._router.navigate(['/storage-plans', 'new']);
  }

  open(row: CinemaServiceAgent.StoragePlanListItemDTO): void {
    this._router.navigate(['/storage-plans', row.id]);
  }
}

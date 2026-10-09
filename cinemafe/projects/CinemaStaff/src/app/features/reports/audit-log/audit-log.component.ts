import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { Store } from '@ngrx/store';
import { Subject } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import {
  AUDIT_LOG_FILTER_FIELDS,
  EmptyStateComponent,
  FilterBarComponent,
  SharedModule,
  CinemaServiceAgent,
  StatusPillComponent,
  hideLoading,
  showException,
  showLoading,
} from 'CinemaLib';
import { TheaterContextService } from '../../../core/theater-context.service';

/** Paged audit trail of sensitive staff actions (overrides, refunds, voids, ...), filterable by action and date. */
@Component({
  selector: 'staff-audit-log',
  standalone: true,
  imports: [SharedModule, StatusPillComponent, EmptyStateComponent, FilterBarComponent],
  templateUrl: './audit-log.component.html',
  styleUrl: './audit-log.component.scss',
})
export class AuditLogComponent implements OnInit {
  readonly pageSize = 20;

  readonly filterFields = AUDIT_LOG_FILTER_FIELDS;

  searchForm: FormGroup;
  rows: CinemaServiceAgent.AuditLogDTO[] = [];
  total = 0;
  pageIndex = 0; // 0-based for the paginator
  loading = false;

  private readonly _theaterContext = inject(TheaterContextService);
  private readonly _filterChange$ = new Subject<void>();

  constructor(
    private _report: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _store: Store<any>,
    private _cd: ChangeDetectorRef,
  ) {
    this.searchForm = this._fb.group({ action: [''], from: [''], to: [''] });

    // Reload when an Admin switches the topbar theater.
    effect(() => {
      this._theaterContext.currentTheaterId();
      this.pageIndex = 0;
      this.load();
    });
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
    const filters: { [key: string]: string } = {};
    const theaterId = this._theaterContext.currentTheaterId();
    if (theaterId) {
      filters['theaterId'] = theaterId;
    }
    const raw = this.searchForm.value as Record<string, string>;
    for (const key of Object.keys(raw)) {
      const value = (raw[key] ?? '').toString().trim();
      if (value) {
        filters[key] = value;
      }
    }
    this.loading = true;
    this._store.dispatch(showLoading());
    this._report.getAuditLog(CinemaServiceAgent.PagingSearchDTO.fromJS({
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
}

import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { Router, RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { Subject } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import {
  ComplaintCategoryValues, ComplaintStatusValues, EmptyStateComponent, FilterBarComponent, FilterBarField, SharedModule, CinemaServiceAgent,
  SELLER_ROLES, StatusPillComponent, complaintCategoryLabel, hideLoading, selectCurrentUser, showException, showLoading,
} from 'CinemaLib';
import { TheaterContextService } from '../../core/theater-context.service';
import { ComplaintDialogComponent, ComplaintDialogData } from './complaint.dialog';
import { complaintFilters } from './customer-service.logic';

/** Paged complaints of the caller's theaters (an Admin's picked theater when set) with status / category filters. */
@Component({
  selector: 'staff-complaint-list',
  standalone: true,
  imports: [SharedModule, RouterLink, StatusPillComponent, EmptyStateComponent, FilterBarComponent],
  templateUrl: './complaint-list.component.html',
  styleUrl: './complaint-list.component.scss',
})
export class ComplaintListComponent implements OnInit {
  readonly pageSize = 10;
  readonly categoryLabel = complaintCategoryLabel;

  readonly filterFields: FilterBarField[] = [
    { key: 'status', type: 'select', labelKey: 'common.status', options: ComplaintStatusValues.map(s => ({ value: String(s.value), labelKey: s.name })) },
    { key: 'category', type: 'select', labelKey: 'customerService.complaint.category', options: ComplaintCategoryValues.map(c => ({ value: String(c.value), labelKey: c.name })) },
  ];

  searchForm: FormGroup;
  rows: CinemaServiceAgent.ComplaintDTO[] = [];
  total = 0;
  pageIndex = 0;
  loading = false;

  private readonly _api = inject(CinemaServiceAgent.HttpService);
  private readonly _theater = inject(TheaterContextService);
  private readonly _store = inject(Store);
  private readonly _user = this._store.selectSignal(selectCurrentUser);
  private readonly _router = inject(Router);
  private readonly _dialog = inject(MatDialog);
  private readonly _cd = inject(ChangeDetectorRef);
  private readonly _filterChange$ = new Subject<void>();

  constructor(fb: FormBuilder) {
    this.searchForm = fb.group({ status: [''], category: [''] });
    effect(() => {
      this._theater.currentTheaterId();
      this.pageIndex = 0;
      this.load();
    });
  }

  /** Only sellers may open the lookup (a regional manager reaches complaints only). */
  get canLookup(): boolean {
    return SELLER_ROLES.includes(this._user()?.userTypeName ?? '');
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
    // An Admin sees the picked theater (all when none); everyone else is scoped by the API.
    const theaterId = this._theater.isAdmin() ? this._theater.currentTheaterId() : null;
    const filters = complaintFilters(this.searchForm.value as Record<string, unknown>, theaterId);
    this.loading = true;
    this._store.dispatch(showLoading());
    this._api.getComplaints(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: this.pageIndex + 1, pageSize: this.pageSize, filters })).subscribe({
      next: result => {
        this.rows = result.results ?? [];
        this.total = result.totalCount ?? 0;
        this._cd.markForCheck();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => {
      this.loading = false;
      this._store.dispatch(hideLoading());
      this._cd.markForCheck();
    });
  }

  create(): void {
    const data: ComplaintDialogData = { theaterId: this._theater.isAdmin() ? (this._theater.currentTheaterId() ?? undefined) : undefined };
    this._dialog.open<ComplaintDialogComponent, ComplaintDialogData, CinemaServiceAgent.ComplaintDTO>(
      ComplaintDialogComponent, { width: '520px', maxWidth: '95vw', data },
    ).afterClosed().subscribe(complaint => {
      if (complaint?.id) {
        this._router.navigate(['/customer-service/complaints', complaint.id]);
      }
    });
  }

  open(row: CinemaServiceAgent.ComplaintDTO): void {
    this._router.navigate(['/customer-service/complaints', row.id]);
  }
}

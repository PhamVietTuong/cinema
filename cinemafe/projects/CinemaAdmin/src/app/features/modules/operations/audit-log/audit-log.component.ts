import { ChangeDetectorRef, Component } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { Observable } from 'rxjs';
import {
  AUDIT_LOG_FILTER_FIELDS,
  BaseTableComponent, TablePage, TableSearchCriteria,
  CinemaServiceAgent,
  FilterBarField,
} from 'CinemaLib';

type Dto = CinemaServiceAgent.AuditLogDTO;

/**
 * Read-only audit trail across every theater (unlike CinemaStaff's version, which is scoped to the
 * topbar's current theater). Shares AUDIT_LOG_FILTER_FIELDS (action/date range) with Staff and adds
 * a theater filter, since an Admin isn't pinned to one theater.
 */
@Component({
  selector: 'app-audit-log',
  standalone: false,
  templateUrl: './audit-log.component.html',
})
export class AuditLogManagementComponent extends BaseTableComponent<Dto> {
  override pageSize = 20;

  theaters: CinemaServiceAgent.TheaterDTO[] = [];

  constructor(
    cd: ChangeDetectorRef,
    fb: FormBuilder,
    router: Router,
    store: Store<any>,
    private _svc: CinemaServiceAgent.HttpService,
  ) {
    super(cd, fb, router, store);
  }

  override ngOnInit(): void {
    super.ngOnInit();
    this._svc.getTheaters(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 200 }))
      .subscribe(r => { this.theaters = r.results ?? []; this._cd.markForCheck(); });
  }

  get filterFields(): FilterBarField[] {
    return [
      {
        key: 'theaterId',
        type: 'select',
        labelKey: 'auditLog.theater',
        options: this.theaters.map(t => ({ value: t.id ?? '', label: t.name ?? '' })),
      },
      ...AUDIT_LOG_FILTER_FIELDS,
    ];
  }

  protected override _createSearchForm(): void {
    this.searchForm = this._formBuilder.group({ theaterId: [''], action: [''], from: [''], to: [''] });
  }

  protected _search(criteria: TableSearchCriteria): Observable<TablePage<Dto>> {
    return this._svc.getAuditLog(CinemaServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: criteria.pageIndex, pageSize: criteria.pageSize, filters: criteria.filters,
    }));
  }
}

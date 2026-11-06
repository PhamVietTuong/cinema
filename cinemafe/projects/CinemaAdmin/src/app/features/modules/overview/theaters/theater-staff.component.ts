import { ChangeDetectorRef, Component, Input } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { MatDialog } from '@angular/material/dialog';
import {
  IdentityServiceAgent,
  UserRole, UserRoles,
  BaseTableComponent, TablePage, TableSearchCriteria,
  DialogService,
  showLoading, hideLoading, showSuccess, showException,
} from 'CinemaLib';
import { UserDialog } from '../users/user.dialog';

interface StaffRow {
  id?: string;
  dto: IdentityServiceAgent.UserDTO;
  name: string;
  email: string;
  phone: string;
  role: UserRole;
  active: boolean;
}

/** Staff roster scoped to a single theater — reuses the same create/edit dialog and delete
 * endpoint as the global Users list, with the theater id pre-filled and locked. */
@Component({
  selector: 'app-theater-staff',
  standalone: false,
  templateUrl: './theater-staff.component.html',
  styleUrls: ['./theater-catalog-tab.scss'],
})
export class TheaterStaffComponent extends BaseTableComponent<StaffRow> {
  @Input({ required: true }) theaterId!: string;
  readonly UserRole = UserRole;

  constructor(
    cd: ChangeDetectorRef,
    fb: FormBuilder,
    router: Router,
    store: Store<any>,
    private _identity: IdentityServiceAgent.HttpService,
    private _dialog: MatDialog,
    private _dialogService: DialogService,
  ) {
    super(cd, fb, router, store);
  }

  protected override _createSearchForm(): void {
    this.searchForm = this._formBuilder.group({});
  }

  protected override _extraFilters(): Record<string, unknown> {
    return { theaterId: this.theaterId };
  }

  protected override _searchStateKey(): string {
    return this._router.url + '#staff';
  }

  protected _search(criteria: TableSearchCriteria): Observable<TablePage<StaffRow>> {
    return this._identity.getUsers(IdentityServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: criteria.pageIndex, pageSize: criteria.pageSize, filters: criteria.filters,
    })).pipe(map(r => ({
      results: (r.results ?? []).map(u => this._toRow(u)),
      totalCount: r.totalCount,
    })));
  }

  private _toRow(u: IdentityServiceAgent.UserDTO): StaffRow {
    return {
      id: u.id,
      dto: u,
      name: u.name ?? '',
      email: u.email ?? '',
      phone: u.phone ?? '',
      role: this._toRole(u.userTypeName),
      active: (u.status ?? IdentityServiceAgent.UserStatus.Active) === IdentityServiceAgent.UserStatus.Active,
    };
  }

  private _toRole(userTypeName?: string): UserRole {
    switch (userTypeName) {
      case UserRoles.Admin: return UserRole.Admin;
      case UserRoles.TheaterStaff: return UserRole.TheaterStaff;
      default: return UserRole.Customer;
    }
  }

  openCreate(): void {
    this._dialog.open(UserDialog, { width: '560px', data: { user: null, lockedTheaterId: this.theaterId } })
      .afterClosed().subscribe(saved => { if (saved) { this.triggerSearch(); } });
  }

  edit(row: StaffRow): void {
    this._dialog.open(UserDialog, { width: '560px', data: { user: row.dto, lockedTheaterId: this.theaterId } })
      .afterClosed().subscribe(saved => { if (saved) { this.triggerSearch(); } });
  }

  delete(id?: string): void {
    if (!id) {
      return;
    }
    this._dialogService.openConfirmDialog({ message: 'common.confirmDelete' })
      .afterClosed().subscribe(confirmed => {
        if (confirmed) {
          this._deleteConfirmed(id);
        }
      });
  }

  private _deleteConfirmed(id: string): void {
    this._store.dispatch(showLoading());
    this._identity.deleteUser(id).subscribe({
      next: () => {
        this._store.dispatch(showSuccess({}));
        this.triggerSearch();
      },
      error: error => this._store.dispatch(showException({ error })),
    }).add(() => this._store.dispatch(hideLoading()));
  }

  initials(name: string): string {
    const parts = name.trim().split(/\s+/);
    return ((parts[0]?.[0] ?? '') + (parts[parts.length - 1]?.[0] ?? '')).toUpperCase();
  }
}

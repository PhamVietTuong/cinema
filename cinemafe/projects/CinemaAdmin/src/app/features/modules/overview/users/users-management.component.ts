import { ChangeDetectorRef, Component } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { MatDialog } from '@angular/material/dialog';
import {
  IdentityServiceAgent,
  UserRole,
  BaseTableComponent, TablePage, TableSearchCriteria,
  DialogService,
  showLoading, hideLoading, showSuccess, showException,
} from 'CinemaLib';
import { UserDialog } from './user.dialog';

interface UserRow {
  id?: string;
  dto: IdentityServiceAgent.UserDTO;
  name: string;
  email: string;
  phone: string;
  role: UserRole;
  active: boolean;
  joined: string;
}

@Component({
  selector: 'app-users-management',
  standalone: false,
  templateUrl: './users-management.component.html',
})
export class UsersManagementComponent extends BaseTableComponent {
  readonly UserRole = UserRole;
  readonly roles: UserRole[] = [UserRole.Admin, UserRole.Customer];

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
    this.searchForm = this._formBuilder.group({ search: [''], role: [''] });
  }

  protected _search(criteria: TableSearchCriteria): Observable<TablePage<UserRow>> {
    return this._identity.getUsers(IdentityServiceAgent.PagingSearchDTO.fromJS({
      pageIndex: criteria.pageIndex, pageSize: criteria.pageSize, filters: criteria.filters,
    })).pipe(map(r => ({
      results: (r.results ?? []).map(u => this._toRow(u)),
      totalCount: r.totalCount,
    })));
  }

  private _toRow(u: IdentityServiceAgent.UserDTO): UserRow {
    return {
      id: u.id,
      dto: u,
      name: u.name ?? '',
      email: u.email ?? '',
      phone: u.phone ?? '',
      role: u.userTypeName === 'Admin' ? UserRole.Admin : UserRole.Customer,
      active: (u.status ?? IdentityServiceAgent.UserStatus.Active) === IdentityServiceAgent.UserStatus.Active,
      joined: u.creationTime ? new Date(u.creationTime).toLocaleDateString('vi-VN') : '',
    };
  }

  openCreate(): void {
    this._dialog.open(UserDialog, { width: '480px', data: { user: null } })
      .afterClosed().subscribe(saved => { if (saved) { this.triggerSearch(); } });
  }

  edit(row: UserRow): void {
    this._dialog.open(UserDialog, { width: '480px', data: { user: row.dto } })
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

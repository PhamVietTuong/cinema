import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { IdentityServiceAgent, SharedModule, UserRole } from 'CinemaLib';

interface UserRow {
  name: string;
  email: string;
  phone: string;
  role: UserRole;
  active: boolean;
  joined: string;
}

@Component({
  selector: 'app-users-management',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './users-management.component.html',
  styleUrl: './users-management.component.scss'
})
export class UsersManagementComponent implements OnInit {
  private _identity = inject(IdentityServiceAgent.HttpService);
  private _cdr = inject(ChangeDetectorRef);

  readonly UserRole = UserRole;
  readonly roles: UserRole[] = [UserRole.Admin, UserRole.Customer];

  search = '';
  filterRole: '' | UserRole = '';
  users: UserRow[] = [];

  ngOnInit(): void {
    this._identity.getUsers(IdentityServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 100 }))
      .subscribe({
        next: r => { this.users = (r.results ?? []).map(u => this._toRow(u)); this._cdr.markForCheck(); },
        error: () => { this.users = []; this._cdr.markForCheck(); },
      });
  }

  private _toRow(u: IdentityServiceAgent.UserDTO): UserRow {
    return {
      name: u.name ?? '',
      email: u.email ?? '',
      phone: u.phone ?? '',
      role: u.userTypeName === 'Admin' ? UserRole.Admin : UserRole.Customer,
      active: (u.status ?? IdentityServiceAgent.UserStatus.Active) === IdentityServiceAgent.UserStatus.Active,
      joined: u.creationTime ? new Date(u.creationTime).toLocaleDateString('vi-VN') : '',
    };
  }

  get total(): number { return this.users.length; }
  get adminCount(): number { return this.users.filter(u => u.role === UserRole.Admin).length; }
  get customerCount(): number { return this.users.filter(u => u.role === UserRole.Customer).length; }

  get filtered(): UserRow[] {
    const q = this.search.trim().toLowerCase();
    return this.users.filter(u =>
      (!this.filterRole || u.role === this.filterRole) &&
      (!q || u.name.toLowerCase().includes(q) || u.email.toLowerCase().includes(q)));
  }

  initials(name: string): string {
    const parts = name.trim().split(/\s+/);
    return ((parts[0]?.[0] ?? '') + (parts[parts.length - 1]?.[0] ?? '')).toUpperCase();
  }
}

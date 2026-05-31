import { Component } from '@angular/core';
import { SharedModule } from 'CinemaLib';

interface UserRow {
  name: string;
  email: string;
  phone: string;
  role: 'Admin' | 'Khách Hàng';
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
export class UsersManagementComponent {
  // NOTE: The Identity API currently only exposes the current user's profile
  // (no admin "list users" endpoint). These rows are placeholder/demo data so
  // the page matches the approved design. Wire `users` to a real endpoint when
  // it is added on the backend.
  search = '';
  filterRole = '';

  users: UserRow[] = [
    { name: 'Phạm Việt Tường', email: 'admin@cinema.vn',  phone: '0901 234 567', role: 'Admin',       active: true,  joined: '12/01/2026' },
    { name: 'Nguyễn Văn An',   email: 'an.nguyen@gmail.com', phone: '0912 345 678', role: 'Khách Hàng', active: true,  joined: '03/02/2026' },
    { name: 'Trần Thị Bình',   email: 'binh.tran@gmail.com', phone: '0923 456 789', role: 'Khách Hàng', active: true,  joined: '18/02/2026' },
    { name: 'Lê Minh Châu',    email: 'chau.le@gmail.com',   phone: '0934 567 890', role: 'Khách Hàng', active: false, joined: '25/02/2026' },
    { name: 'Hoàng Quản Trị',  email: 'manager@cinema.vn',   phone: '0945 678 901', role: 'Admin',       active: true,  joined: '01/03/2026' },
    { name: 'Đỗ Thu Hà',       email: 'ha.do@gmail.com',     phone: '0956 789 012', role: 'Khách Hàng', active: true,  joined: '14/03/2026' },
    { name: 'Vũ Đức Duy',      email: 'duy.vu@gmail.com',    phone: '0967 890 123', role: 'Khách Hàng', active: false, joined: '22/03/2026' },
  ];

  get total(): number { return this.users.length; }
  get adminCount(): number { return this.users.filter(u => u.role === 'Admin').length; }
  get customerCount(): number { return this.users.filter(u => u.role === 'Khách Hàng').length; }

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

import { Component } from '@angular/core';
import { SharedModule } from 'CinemaLib';

interface ShowtimeRow {
  movie: string;
  theater: string;
  room: string;
  date: string;
  time: string;
  price: number;
  seatsLeft: number;
  seatsTotal: number;
  status: 'selling' | 'upcoming' | 'ended';
}

@Component({
  selector: 'app-showtimes-management',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './showtimes-management.component.html',
  styleUrl: './showtimes-management.component.scss'
})
export class ShowtimesManagementComponent {
  // NOTE: The backend does not yet expose a "list showtimes" admin endpoint.
  // These rows are placeholder/demo data so the page matches the approved
  // design. Wire `rows` to a CinemaServiceAgent call once the endpoint exists.
  filterMovie = '';
  filterTheater = '';
  filterStatus = '';

  rows: ShowtimeRow[] = [
    { movie: 'Galactic Odyssey', theater: 'CGV Vincom', room: 'Phòng 03', date: '30/05/2026', time: '19:30', price: 90000, seatsLeft: 42, seatsTotal: 120, status: 'selling' },
    { movie: 'The Last Dragon',  theater: 'Lotte Cinema', room: 'Phòng 01', date: '30/05/2026', time: '20:00', price: 85000, seatsLeft: 8,  seatsTotal: 96,  status: 'selling' },
    { movie: 'Neon Hunter',      theater: 'BHD Star',    room: 'Phòng 05', date: '31/05/2026', time: '17:15', price: 80000, seatsLeft: 120, seatsTotal: 120, status: 'upcoming' },
    { movie: 'Shadows of Truth', theater: 'Galaxy',      room: 'Phòng 02', date: '29/05/2026', time: '21:45', price: 95000, seatsLeft: 0,  seatsTotal: 110, status: 'ended' },
    { movie: 'Galactic Odyssey', theater: 'CGV Vincom',  room: 'Phòng 03', date: '31/05/2026', time: '14:00', price: 75000, seatsLeft: 110, seatsTotal: 120, status: 'upcoming' },
    { movie: 'The Last Dragon',  theater: 'BHD Star',    room: 'Phòng 04', date: '30/05/2026', time: '22:30', price: 90000, seatsLeft: 33, seatsTotal: 100, status: 'selling' },
  ];

  get movies(): string[] { return [...new Set(this.rows.map(r => r.movie))]; }
  get theaters(): string[] { return [...new Set(this.rows.map(r => r.theater))]; }

  get filtered(): ShowtimeRow[] {
    return this.rows.filter(r =>
      (!this.filterMovie || r.movie === this.filterMovie) &&
      (!this.filterTheater || r.theater === this.filterTheater) &&
      (!this.filterStatus || r.status === this.filterStatus));
  }

  statusLabel(s: ShowtimeRow['status']): string {
    return s === 'selling' ? 'Đang Bán' : s === 'upcoming' ? 'Sắp Chiếu' : 'Đã Kết Thúc';
  }
  statusClass(s: ShowtimeRow['status']): string {
    return s === 'selling' ? 'ad-pill--success' : s === 'upcoming' ? 'ad-pill--warn' : 'ad-pill--neutral';
  }
}

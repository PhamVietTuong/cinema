import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';

@Component({
  selector: 'app-theaters',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './theaters.component.html',
  styleUrl: './theaters.component.scss',
})
export class TheatersComponent implements OnInit {
  private _cinema = inject(CinemaServiceAgent.HttpService);
  private _cdr = inject(ChangeDetectorRef);
  private _translate = inject(TranslateService);

  theaters: CinemaServiceAgent.TheaterDTO[] = [];
  city = '';
  loading = true;

  locating = false;
  geoError = '';
  private _lat?: number;
  private _lng?: number;

  ngOnInit(): void {
    this._cinema.getTheaters(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 100 }))
      .subscribe({
        next: r => { this.theaters = r.results ?? []; this.loading = false; this._cdr.markForCheck(); },
        error: () => { this.loading = false; this._cdr.markForCheck(); },
      });
  }

  get cities(): string[] {
    return [...new Set(this.theaters.map(t => t.city ?? '').filter(Boolean))];
  }

  get hasLocation(): boolean { return this._lat != null && this._lng != null; }

  get filtered(): CinemaServiceAgent.TheaterDTO[] {
    const list = this.city ? this.theaters.filter(t => t.city === this.city) : [...this.theaters];
    if (this.hasLocation) {
      // Theaters with a known distance first, nearest → farthest.
      list.sort((a, b) => {
        const da = this.distanceKm(a); const db = this.distanceKm(b);
        if (da == null) return db == null ? 0 : 1;
        if (db == null) return -1;
        return da - db;
      });
    }
    return list;
  }

  /** Great-circle distance (km) from the user to a theater, or null if unknown. */
  distanceKm(t: CinemaServiceAgent.TheaterDTO): number | null {
    if (this._lat == null || this._lng == null || t.latitude == null || t.longitude == null) { return null; }
    const R = 6371;
    const dLat = (t.latitude - this._lat) * Math.PI / 180;
    const dLng = (t.longitude - this._lng) * Math.PI / 180;
    const a = Math.sin(dLat / 2) ** 2
      + Math.cos(this._lat * Math.PI / 180) * Math.cos(t.latitude * Math.PI / 180) * Math.sin(dLng / 2) ** 2;
    return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  }

  findNearest(): void {
    if (!navigator.geolocation) { this.geoError = this._translate.instant('theaters.geoNotSupported'); return; }
    this.locating = true; this.geoError = '';
    navigator.geolocation.getCurrentPosition(
      pos => { this._lat = pos.coords.latitude; this._lng = pos.coords.longitude; this.locating = false; this._cdr.markForCheck(); },
      () => { this.locating = false; this.geoError = this._translate.instant('theaters.geoFailed'); this._cdr.markForCheck(); },
      { enableHighAccuracy: false, timeout: 10000 },
    );
  }
}

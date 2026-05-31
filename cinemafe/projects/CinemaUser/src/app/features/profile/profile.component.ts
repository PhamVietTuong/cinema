import { Component, OnInit, Inject } from '@angular/core';
import { Observable } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { Store } from '@ngrx/store';
import { SharedModule, selectCurrentUser, API_BASE_URL } from 'CinemaLib';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
})
export class ProfileComponent implements OnInit {
  user$: Observable<any>;
  invoices: any[] = [];

  constructor(
    private _store: Store,
    private _http: HttpClient,
    @Inject(API_BASE_URL) private _apiUrl: string,
  ) {
    this.user$ = this._store.select(selectCurrentUser);
  }

  ngOnInit(): void {
    this._http.get<any>(`${this._apiUrl}/invoices/my`).subscribe({
      next: res => this.invoices = res.items ?? [],
      error: () => {},
    });
  }
}

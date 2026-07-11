import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { ImageUploadService } from '../../shared/image-upload.service';
import { TheaterRoomsComponent } from './theater-rooms.component';
import { TheaterRoomTypesComponent } from './theater-room-types.component';
import { TheaterSeatTypesComponent } from './theater-seat-types.component';
import { TheaterFoodComponent } from './theater-food.component';
import { TheaterTimeSlotsComponent } from './theater-time-slots.component';
import { TheaterTicketPricesComponent } from './theater-ticket-prices.component';

/** Theater detail page: info on top, per-theater management tabs below. */
@Component({
  selector: 'app-theater-detail',
  standalone: true,
  imports: [
    SharedModule,
    TheaterRoomsComponent,
    TheaterRoomTypesComponent,
    TheaterSeatTypesComponent,
    TheaterFoodComponent,
    TheaterTimeSlotsComponent,
    TheaterTicketPricesComponent,
  ],
  templateUrl: './theater-detail.component.html',
  styleUrl: './theater-detail.component.scss',
})
export class TheaterDetailComponent implements OnInit {
  theaterId = '';
  saving = false;
  uploading = false;
  uploadError = '';
  form: FormGroup;

  constructor(
    private _route: ActivatedRoute,
    private _router: Router,
    private _cinemaService: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _cdr: ChangeDetectorRef,
    private _upload: ImageUploadService,
  ) {
    this.form = this._fb.group({
      name: ['', Validators.required],
      city: ['', Validators.required],
      address: ['', Validators.required],
      phone: ['', Validators.pattern(/^(?:\+84|0)\d{9,10}$/)],
      email: ['', Validators.email],
      imageUrl: [''],
      latitude: [null],
      longitude: [null],
    });
  }

  ngOnInit(): void {
    this.theaterId = this._route.snapshot.paramMap.get('id') ?? '';
    this.loadTheater();
  }

  loadTheater(): void {
    this._cinemaService.getTheater(this.theaterId).subscribe(t => {
      this.form.patchValue({
        name: t.name, city: t.city, address: t.address, phone: t.phone, imageUrl: t.imageUrl,
        latitude: t.latitude, longitude: t.longitude,
      });
      this._cdr.markForCheck();
    });
  }

  onPickImage(e: Event): void {
    const file = (e.target as HTMLInputElement).files?.[0];
    if (!file) { return; }
    this.uploading = true; this.uploadError = '';
    this._upload.upload(file).subscribe({
      next: url => { this.form.patchValue({ imageUrl: url }); this.uploading = false; this._cdr.markForCheck(); },
      error: () => { this.uploadError = 'Tải ảnh thất bại.'; this.uploading = false; this._cdr.markForCheck(); },
    });
  }

  save(): void {
    if (!this.form.valid) { this.form.markAllAsTouched(); return; }
    this.saving = true;
    this._cinemaService.updateTheater(
      CinemaServiceAgent.UpdateTheaterRequest.fromJS({ ...this.form.value, id: this.theaterId }))
      .subscribe({
        next: () => { this.saving = false; this._cdr.markForCheck(); },
        error: () => { this.saving = false; this._cdr.markForCheck(); },
      });
  }

  goBack(): void {
    this._router.navigate(['/theaters']);
  }
}

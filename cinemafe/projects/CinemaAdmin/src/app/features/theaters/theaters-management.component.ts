import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { SharedModule, CinemaServiceAgent } from 'CinemaLib';
import { ImageUploadService } from '../../shared/image-upload.service';

@Component({
  selector: 'app-theaters-management',
  standalone: true,
  imports: [SharedModule],
  templateUrl: './theaters-management.component.html',
  styleUrl: './theaters-management.component.scss'
})
export class TheatersManagementComponent implements OnInit {
  theaters: CinemaServiceAgent.TheaterDTO[] = [];
  search = '';
  showForm = false;
  editingId: string | null = null;
  uploading = false;
  uploadError = '';
  form: FormGroup;

  constructor(
    private _cinemaService: CinemaServiceAgent.HttpService,
    private _fb: FormBuilder,
    private _cdr: ChangeDetectorRef,
    private _upload: ImageUploadService,
  ) {
    this.form = this._fb.group({
      name: ['', Validators.required],
      city: ['', Validators.required],
      address: ['', Validators.required],
      phone: [''],
      email: [''],
      imageUrl: [''],
    });
  }

  ngOnInit(): void { this.loadTheaters(); }

  get filtered(): CinemaServiceAgent.TheaterDTO[] {
    const q = this.search.trim().toLowerCase();
    if (!q) return this.theaters;
    return this.theaters.filter(t =>
      (t.name ?? '').toLowerCase().includes(q) ||
      (t.city ?? '').toLowerCase().includes(q));
  }

  loadTheaters(): void {
    this._cinemaService.getTheaters(CinemaServiceAgent.PagingSearchDTO.fromJS({ pageIndex: 1, pageSize: 100 }))
      .subscribe(t => { this.theaters = t.results ?? []; this._cdr.markForCheck(); });
  }

  openCreate(): void { this.cancelEdit(); this.showForm = true; }

  editTheater(t: CinemaServiceAgent.TheaterDTO): void {
    this.editingId = t.id ?? null;
    this.showForm = true;
    this.form.patchValue({ name: t.name, city: t.city, address: t.address, phone: t.phone, imageUrl: t.imageUrl });
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

  saveTheater(): void {
    if (!this.form.valid) { this.form.markAllAsTouched(); return; }
    const obs = this.editingId
      ? this._cinemaService.updateTheater(CinemaServiceAgent.UpdateTheaterRequest.fromJS({ ...this.form.value, id: this.editingId }))
      : this._cinemaService.createTheater(CinemaServiceAgent.CreateTheaterRequest.fromJS(this.form.value));
    obs.subscribe(() => { this.loadTheaters(); this.cancelEdit(); });
  }

  cancelEdit(): void { this.showForm = false; this.editingId = null; this.form.reset(); }
}

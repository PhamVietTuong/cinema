import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { CinemaServiceAgent } from 'CinemaLib';

/** Uploads an image file to the API and resolves to its absolute URL. */
@Injectable({ providedIn: 'root' })
export class ImageUploadService {
  private _svc = inject(CinemaServiceAgent.HttpService);

  upload(file: File): Observable<string> {
    return this._svc.uploadImage({ data: file, fileName: file.name }).pipe(map(r => r.url ?? ''));
  }
}

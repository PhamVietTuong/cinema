import { TestBed } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { Store } from '@ngrx/store';
import { provideTranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { CinemaServiceAgent, LookupCacheService } from 'CinemaLib';
import { ImageUploadService } from '../../../../shared/image-upload.service';
import { MovieDialog } from './movie.dialog';
import { RichTextEditorComponent } from '../../../../shared/rich-text-editor.component';

describe('MovieDialog', () => {
  it('does not send credits when updating, so the server leaves them untouched', () => {
    const requests: CinemaServiceAgent.UpdateMovieRequest[] = [];
    const movie = CinemaServiceAgent.MovieDTO.fromJS({
      id: 'mv-1', title: 'T', description: 'D', duration: 90, releaseDate: new Date(2027, 0, 1), ageRestrictionId: 'age-1', movieTypeIds: [],
    });
    TestBed.configureTestingModule({
      imports: [ReactiveFormsModule, RichTextEditorComponent],
      declarations: [MovieDialog],
      providers: [
        provideTranslateService({ fallbackLang: 'vi' }),
        { provide: MAT_DIALOG_DATA, useValue: { movie } },
        { provide: MatDialogRef, useValue: { close: () => undefined } },
        { provide: Store, useValue: { dispatch: () => undefined } },
        { provide: ImageUploadService, useValue: {} },
        { provide: LookupCacheService, useValue: { ageRestrictions: () => of({ results: [] }), movieTypes: () => of({ results: [] }) } },
        {
          provide: CinemaServiceAgent.HttpService,
          useValue: {
            updateMovie: (r: CinemaServiceAgent.UpdateMovieRequest) => {
              requests.push(r);
              return of(undefined);
            },
          },
        },
      ],
    });
    TestBed.overrideComponent(MovieDialog, { set: { template: '', templateUrl: undefined, styleUrl: undefined } });
    const cmp = TestBed.createComponent(MovieDialog).componentInstance;
    cmp.save();
    expect(requests.length).toBe(1);
    expect(requests[0].credits).toBeUndefined();
    expect(requests[0].id).toBe('mv-1');
  });
});

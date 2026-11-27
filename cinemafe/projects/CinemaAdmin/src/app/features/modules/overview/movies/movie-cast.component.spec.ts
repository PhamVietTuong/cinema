import { TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { provideTranslateService, TranslatePipe } from '@ngx-translate/core';
import { of } from 'rxjs';
import { CinemaServiceAgent } from 'CinemaLib';
import { ImageUploadService } from '../../../../shared/image-upload.service';
import { MovieCastComponent } from './movie-cast.component';

const Director = CinemaServiceAgent.CreditType.Director;
const Actor = CinemaServiceAgent.CreditType.Actor;

function setup() {
  const movie = CinemaServiceAgent.MovieDetailDTO.fromJS({
    id: 'mv-1',
    title: 'Test movie',
    description: 'desc',
    duration: 100,
    ageRestrictionId: 'age-1',
    movieTypeIds: ['t-1', 't-2'],
    director: 'Free text',
    cast: 'Free cast',
    language: 'en',
    credits: [
      { personId: 'p-2', personName: 'Actor Two', creditType: Actor, characterName: 'Hero', displayOrder: 0 },
      { personId: 'p-1', personName: 'Dir One', creditType: Director, displayOrder: 1 },
      { personId: 'p-1', personName: 'Dir One', creditType: Actor, characterName: 'Cameo', displayOrder: 2 },
    ],
  });
  const requests: CinemaServiceAgent.UpdateMovieRequest[] = [];
  const dispatched: unknown[] = [];
  TestBed.configureTestingModule({
    declarations: [MovieCastComponent],
    imports: [FormsModule, MatIconModule, TranslatePipe],
    providers: [
      provideTranslateService({ fallbackLang: 'vi' }),
      {
        provide: CinemaServiceAgent.HttpService,
        useValue: {
          getMovie: () => of(movie),
          getPeople: () => of({ results: [], totalCount: 0 }),
          updateMovie: (r: CinemaServiceAgent.UpdateMovieRequest) => {
            requests.push(r);
            return of(undefined);
          },
        },
      },
      { provide: Store, useValue: { dispatch: (a: unknown) => dispatched.push(a) } },
      { provide: ImageUploadService, useValue: {} },
      { provide: Router, useValue: { navigate: () => Promise.resolve(true) } },
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'mv-1' } } } },
    ],
  });
  const fixture = TestBed.createComponent(MovieCastComponent);
  fixture.detectChanges();
  return { cmp: fixture.componentInstance, requests, dispatched };
}

describe('MovieCastComponent', () => {
  it('saves credits Director first then Actor, with displayOrder 0..n and the movie scalars kept', () => {
    const { cmp, requests } = setup();
    cmp.rows[0].characterName = 'Hero 2';
    cmp.save();
    expect(requests.length).toBe(1);
    const r = requests[0];
    expect(r.movieTypeIds).toEqual(['t-1', 't-2']);
    expect(r.ageRestrictionId).toBe('age-1');
    expect(r.title).toBe('Test movie');
    expect(r.duration).toBe(100);
    expect(r.director).toBe('Free text');
    expect(r.cast).toBe('Free cast');
    const credits = r.credits ?? [];
    expect(credits.map(c => c.creditType)).toEqual([Director, Actor, Actor]);
    expect(credits.map(c => c.displayOrder)).toEqual([0, 1, 2]);
    expect(credits[0].characterName).toBeUndefined();
    expect(credits[1].characterName).toBe('Hero 2');
  });

  it('gives a newly created person no personId', () => {
    const { cmp, requests } = setup();
    cmp.newName = 'Brand New';
    cmp.newType = Actor;
    cmp.confirmCreate();
    const row = cmp.rows[cmp.rows.length - 1];
    expect(row.personId).toBeNull();
    cmp.save();
    const sent = (requests[0].credits ?? []).find(c => c.personName === 'Brand New');
    expect(sent?.personId).toBeUndefined();
  });

  it('refuses to switch a row into a role the person already has', () => {
    const { cmp } = setup();
    const director = cmp.rows.find(r => r.personId === 'p-1' && r.creditType === Director)!;
    cmp.setType(director, Actor);
    expect(director.creditType).toBe(Director);
  });

  it('keeps rows of the same person in sync when the name is edited', () => {
    const { cmp } = setup();
    const [a, b] = cmp.rows.filter(r => r.personId === 'p-1');
    a.name = 'Renamed';
    cmp.onPersonEdited(a, 'name');
    expect(b.name).toBe('Renamed');
  });
});

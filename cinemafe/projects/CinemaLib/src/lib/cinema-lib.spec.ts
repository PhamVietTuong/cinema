import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CinemaLib } from './cinema-lib';

describe('CinemaLib', () => {
  let component: CinemaLib;
  let fixture: ComponentFixture<CinemaLib>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CinemaLib],
    }).compileComponents();

    fixture = TestBed.createComponent(CinemaLib);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

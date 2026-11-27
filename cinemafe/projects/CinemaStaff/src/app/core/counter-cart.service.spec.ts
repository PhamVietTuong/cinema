import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { TranslateService } from '@ngx-translate/core';
import { Subject } from 'rxjs';
import { CinemaServiceAgent, PaymentServiceAgent, SeatLockSessionService } from 'CinemaLib';
import { CounterCartService, CounterFood } from './counter-cart.service';
import { TheaterContextService } from './theater-context.service';
import { signal } from '@angular/core';

const food = (name: string, over: Partial<CounterFood> = {}): CounterFood => ({ id: name, name, ...over }) as CounterFood;

describe('CounterCartService.foodGroups', () => {
  let svc: CounterCartService;

  beforeEach(() => {
    const sessionStub = { locked$: new Subject(), unlocked$: new Subject(), lockFailed$: new Subject(), booked$: new Subject(), stop: () => Promise.resolve() };
    TestBed.configureTestingModule({
      providers: [
        CounterCartService,
        { provide: PaymentServiceAgent.HttpService, useValue: {} },
        { provide: CinemaServiceAgent.HttpService, useValue: {} },
        { provide: SeatLockSessionService, useValue: sessionStub },
        { provide: MatDialog, useValue: {} },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: TheaterContextService, useValue: { currentTheaterId: signal(null), isAdmin: signal(false) } },
      ],
    });
    svc = TestBed.inject(CounterCartService);
  });

  it('partitions food, drinks and combos in that order', () => {
    svc.foods.set([
      food('Popcorn'),
      food('Cola', { isDrink: true }),
      food('Combo', { isCombo: true }),
      food('Fries'),
    ]);
    const groups = svc.foodGroups();
    expect(groups.map(g => g.key)).toEqual(['food', 'drink', 'combo']);
    expect(groups[0].foods.map(f => f.name)).toEqual(['Popcorn', 'Fries']);
    expect(groups[1].foods.map(f => f.name)).toEqual(['Cola']);
    expect(groups[2].foods.map(f => f.name)).toEqual(['Combo']);
  });

  it('a combo flagged as drink is shown under combos only', () => {
    svc.foods.set([food('Combo', { isCombo: true, isDrink: true })]);
    expect(svc.foodGroups().map(g => g.key)).toEqual(['combo']);
  });

  it('drops empty groups, and yields none for an empty catalog', () => {
    svc.foods.set([food('Cola', { isDrink: true })]);
    expect(svc.foodGroups().map(g => g.key)).toEqual(['drink']);
    svc.foods.set([]);
    expect(svc.foodGroups()).toEqual([]);
  });
});

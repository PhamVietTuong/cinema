import { TestBed } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Store } from '@ngrx/store';
import { provideTranslateService, TranslatePipe } from '@ngx-translate/core';
import { CinemaServiceAgent, DialogService } from 'CinemaLib';
import { TicketPriceDialog, TicketPriceDialogData } from './ticket-price.dialog';

const MONDAY = CinemaServiceAgent.DayOfWeek.Monday;

function setup(precedingMultipliers: Record<number, number> | null) {
  const data: TicketPriceDialogData = {
    theaterId: 'th-1',
    roomTypeId: 'rt-1',
    roomTypeName: '2D',
    timeSlot: CinemaServiceAgent.TimeSlotDTO.fromJS({ id: 'ts-l', name: 'Khuya', startTime: '22:00', endTime: '23:59' }),
    day: MONDAY,
    precedingMultipliers,
  };
  TestBed.configureTestingModule({
    declarations: [TicketPriceDialog],
    imports: [
      ReactiveFormsModule, MatDialogModule, MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule,
      TranslatePipe,
    ],
    providers: [
      provideTranslateService({ fallbackLang: 'vi' }),
      { provide: MAT_DIALOG_DATA, useValue: data },
      { provide: MatDialogRef, useValue: { close: () => undefined } },
      { provide: CinemaServiceAgent.HttpService, useValue: {} },
      { provide: Store, useValue: { dispatch: () => undefined } },
      { provide: DialogService, useValue: {} },
    ],
  });
  const fixture = TestBed.createComponent(TicketPriceDialog);
  fixture.detectChanges();
  const setMultiplier = (value: number) => {
    fixture.componentInstance.form.controls['priceMultiplier'].setValue(value);
    fixture.detectChanges();
  };
  const warningCount = () => fixture.nativeElement.querySelectorAll('[data-testid="late-night-warning"]').length;
  return { fixture, setMultiplier, warningCount };
}

describe('TicketPriceDialog late-night warning', () => {
  it('warns above the preceding multiplier and not below it', () => {
    const { fixture, setMultiplier, warningCount } = setup({ [MONDAY]: 1.2 });
    setMultiplier(1.3);
    expect(fixture.componentInstance.showLateNightWarning).toBe(true);
    expect(warningCount()).toBe(1);
    setMultiplier(0.85);
    expect(fixture.componentInstance.showLateNightWarning).toBe(false);
    expect(warningCount()).toBe(0);
  });

  it('never warns when the parent supplies no comparison values', () => {
    const { fixture, setMultiplier, warningCount } = setup(null);
    setMultiplier(5);
    expect(fixture.componentInstance.showLateNightWarning).toBe(false);
    expect(warningCount()).toBe(0);
  });
});

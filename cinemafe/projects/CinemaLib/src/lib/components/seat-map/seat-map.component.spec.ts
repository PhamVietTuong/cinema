import { TestBed } from '@angular/core/testing';
import { ChangeDetectorRef, Component, inject } from '@angular/core';
import { provideTranslateService } from '@ngx-translate/core';
import { PaymentServiceAgent } from '../../services/payment-http.service';
import { SeatMapComponent } from './seat-map.component';
import { SelectableSeat } from './seat-selection';

const seat = (over: Partial<SelectableSeat>): SelectableSeat =>
  ({ id: 's', rowName: 'A', colIndex: 1, displayNumber: 1, status: PaymentServiceAgent.SeatStatus.Available, ...over }) as SelectableSeat;

@Component({
  standalone: true,
  imports: [SeatMapComponent],
  template: `<cl-seat-map [seats]="seats" (seatToggle)="toggled = $event" />`,
})
class HostComponent {
  private readonly _cdr = inject(ChangeDetectorRef);
  seats: SelectableSeat[] = [];
  toggled: SelectableSeat | null = null;

  /** What an owning screen does after changing seats: mark itself for check (the app is zoneless). */
  touch(): void {
    this._cdr.markForCheck();
  }
}

describe('SeatMapComponent (OnPush view model)', () => {
  const make = (seats: SelectableSeat[]) => {
    TestBed.configureTestingModule({ imports: [HostComponent], providers: [provideTranslateService()] });
    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.seats = seats;
    fixture.detectChanges();
    return fixture;
  };
  const cells = (root: HTMLElement) => Array.from(root.querySelectorAll<HTMLElement>('.seat'));

  it('renders one box per seat with its label and state, merging a double pair', () => {
    const f = make([
      seat({ id: '1', colIndex: 1, displayNumber: 1 }),
      seat({ id: '2', colIndex: 2, displayNumber: 2, status: PaymentServiceAgent.SeatStatus.Occupied }),
      seat({ id: '3', colIndex: 3, displayNumber: 3, seatGroupId: 'g', isDouble: true }),
      seat({ id: '4', colIndex: 4, displayNumber: 3, seatGroupId: 'g', isDouble: true }),
    ]);
    const boxes = cells(f.nativeElement);
    expect(boxes.length).toBe(3);
    expect(boxes[0].textContent).toBe('A1');
    expect(boxes[0].classList).toContain('available');
    expect(boxes[1].classList).toContain('occupied');
    expect(boxes[2].classList).toContain('double-wide');
  });

  it('repaints when the owner mutates seat flags in place (selection, lock, category block)', () => {
    const seats = [seat({ id: '1' }), seat({ id: '2', colIndex: 2, displayNumber: 2 })];
    const f = make(seats);
    seats[0].isSelected = true;
    seats[1].isAllowedForPatronCategory = false;
    f.componentInstance.touch();
    f.detectChanges();
    let boxes = cells(f.nativeElement);
    expect(boxes[0].classList).toContain('selected');
    expect(boxes[1].classList).toContain('unavailable-category');

    seats[0].isSelected = false;
    seats[1].isLocked = true;
    f.componentInstance.touch();
    f.detectChanges();
    boxes = cells(f.nativeElement);
    expect(boxes[0].classList).toContain('available');
    expect(boxes[1].classList).toContain('locked');
  });

  it('repaints when the seats array is replaced and emits the clicked seat', () => {
    const f = make([seat({ id: '1' })]);
    f.componentInstance.seats = [seat({ id: 'x', rowName: 'B', displayNumber: 7, colIndex: 7 })];
    f.componentInstance.touch();
    f.detectChanges();
    const boxes = cells(f.nativeElement);
    expect(boxes.length).toBe(1);
    expect(boxes[0].textContent).toBe('B7');
    boxes[0].click();
    expect(f.componentInstance.toggled?.id).toBe('x');
  });
});

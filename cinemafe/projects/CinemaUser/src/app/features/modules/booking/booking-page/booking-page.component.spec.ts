import { BookingPageComponent } from './booking-page.component';

describe('BookingPageComponent', () => {
  it('reads showTimeId and roomId off the query params', () => {
    const route = { snapshot: { queryParams: { showTimeId: 'st-1', roomId: 'room-1' } } };

    const c = new BookingPageComponent(route as never);
    c.ngOnInit();

    expect(c.showTimeId).toBe('st-1');
    expect(c.roomId).toBe('room-1');
  });

  it('defaults to empty strings when the query params are missing', () => {
    const route = { snapshot: { queryParams: {} } };

    const c = new BookingPageComponent(route as never);
    c.ngOnInit();

    expect(c.showTimeId).toBe('');
    expect(c.roomId).toBe('');
  });
});

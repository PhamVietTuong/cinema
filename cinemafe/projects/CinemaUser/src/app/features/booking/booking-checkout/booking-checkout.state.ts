/**
 * Shape carried from BookingPageComponent to BookingCheckoutComponent via router navigation state.
 * Deliberately flat/primitives-only: `history.state` is structured-cloned, so NSwag class
 * instances (SeatDTO, PatronCategoryDTO, ...) would lose their prototypes and break `fromJS`/getters.
 */
export interface BookingCheckoutSeat {
  seatId: string;
  label: string;
  /** "Standard" or "Double". */
  seatKind: string;
  /** The seat's "from" price shown on the seat map before a category was resolved. */
  basePrice: number;
  /** The resolved patron-category price actually charged (server-authoritative). */
  price: number;
  patronCategoryId: string;
  patronCategoryName: string;
}

export interface BookingCheckoutFood {
  foodAndDrinkId: string;
  name: string;
  unitPrice: number;
  quantity: number;
}

export interface BookingCheckoutState {
  showTimeId: string;
  roomId: string;
  /** In click order. */
  seats: BookingCheckoutSeat[];
  /** Only entries with quantity > 0. */
  foods: BookingCheckoutFood[];
}

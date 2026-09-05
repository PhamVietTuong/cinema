/**
 * Shape carried from BookingPageComponent to BookingCheckoutComponent via router navigation state.
 * Deliberately flat/primitives-only: `history.state` is structured-cloned, so NSwag class
 * instances (SeatDTO, PatronCategoryDTO, ...) would lose their prototypes and break `fromJS`/getters.
 */
export interface BookingCheckoutSeat {
  seatId: string;
  label: string;
  seatTypeName: string;
  basePrice: number;
  /** Price after the assigned patron category's discount. */
  price: number;
  patronCategoryId: string;
  patronCategoryName: string;
  discountPercent: number;
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

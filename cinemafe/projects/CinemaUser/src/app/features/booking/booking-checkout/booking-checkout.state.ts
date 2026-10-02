/**
 * Shape carried from BookingPageComponent to BookingCheckoutComponent via router navigation state.
 * Deliberately flat/primitives-only: `history.state` is structured-cloned, so NSwag class
 * instances (SeatDTO, PatronCategoryDTO, ...) would lose their prototypes and break `fromJS`/getters.
 */
export interface BookingCheckoutSeat {
  seatId: string;
  label: string;
  /** Mirrors the backend `SeatKind` enum member name — "Standard" or "Double" — not the
   * user-facing label, which comes from `booking.seats.kindStandard`/`kindDouble` via `seatKindLabel()`. */
  seatKind: string;
  /** The seat's "from" price shown on the seat map before a category was resolved. */
  basePrice: number;
  /** The resolved patron-category price actually charged (server-authoritative). */
  price: number;
  patronCategoryId: string;
  patronCategoryName: string;
  /** Shared by both physical seats of a Double pair; undefined for a Standard seat. */
  seatGroupId?: string;
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

const PENDING_KEY = 'pendingCheckout';

/** Parks the order across the login round-trip (router state does not survive it). */
export function stashPendingCheckout(state: BookingCheckoutState): void {
  try {
    sessionStorage.setItem(PENDING_KEY, JSON.stringify(state));
  } catch {
    // Storage unavailable — checkout falls back to sending the user back to seat selection.
  }
}

/** Returns and clears the order parked by `stashPendingCheckout`, if any. */
export function takePendingCheckout(): Partial<BookingCheckoutState> | undefined {
  try {
    const raw = sessionStorage.getItem(PENDING_KEY);
    sessionStorage.removeItem(PENDING_KEY);
    return raw ? JSON.parse(raw) as BookingCheckoutState : undefined;
  } catch {
    return undefined;
  }
}

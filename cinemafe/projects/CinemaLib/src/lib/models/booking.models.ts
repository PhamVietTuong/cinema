export interface Theater {
  id: number;
  name: string;
  address: string;
  city: string;
  phone?: string;
  imageUrl?: string;
  roomCount: number;
  isActive: boolean;
}

export interface Seat {
  id: number;
  rowName: string;
  colIndex: number;
  seatTypeId: number;
  seatTypeName: string;
  seatTypeColor: string;
  status: SeatStatus;
  price: number;
  isLocked: boolean;
  isSelected?: boolean;
}

export enum SeatStatus {
  Available = 0,
  Reserved = 1,
  Occupied = 2,
}

export interface CreateBookingRequest {
  showTimeId: number;
  roomId: number;
  seats: BookingSeatItem[];
  foods: BookingFoodItem[];
  discountCode?: string;
  paymentMethod: string;
}

export interface BookingSeatItem {
  seatId: number;
  ticketTypeId: number;
}

export interface BookingFoodItem {
  foodAndDrinkId: number;
  quantity: number;
}

export interface BookingResult {
  invoiceId: string;
  invoiceCode: string;
  totalAmount: number;
  discountAmount: number;
  finalAmount: number;
  status: number;
  paymentUrl?: string;
  tickets: TicketItem[];
}

export interface TicketItem {
  seatLabel: string;
  seatType: string;
  ticketType: string;
  price: number;
  qrCode?: string;
}

import { StaffServiceAgent } from 'CinemaLib';

type Tender = StaffServiceAgent.PaymentTender;

/** One tender line the cashier is entering (amount in whole dong). */
export interface TenderEntry {
  method: Tender;
  amount: number;
  reference: string;
}

/** One ticket being sold: a seat (or both halves of a double seat) priced under one patron category. */
export interface PosTicket {
  seatIds: string[];
  label: string;
  isDouble: boolean;
  patronCategoryId: string;
  /** Replaces the list price of the whole ticket (needs manager approval). */
  overridePrice?: number;
}

/** A food or combo line. */
export interface PosFoodLine {
  foodAndDrinkId: string;
  name: string;
  unitPrice: number;
  quantity: number;
  /** Replaces the unit price (needs manager approval). */
  overridePrice?: number;
}

/** The (patron category x seat kind) list price row. */
export interface PriceRowLike {
  patronCategoryId?: string;
  price?: number;
}

export interface Settlement {
  due: number;
  /** Everything handed over, cash included. */
  tendered: number;
  /** Still to collect (0 once covered). */
  remaining: number;
  /** Cash to hand back. */
  changeDue: number;
  /** Card / QR amounts alone exceed the amount due: they cannot be refunded as change. */
  nonCashExcess: boolean;
  /** A card / QR line has no reference. */
  missingReference: boolean;
  /** A line has a zero or negative amount. */
  invalidAmount: boolean;
  /** Safe to submit the sale. */
  complete: boolean;
  /** At least one cash line (the drawer must be open). */
  usesCash: boolean;
}

const round = (n: number): number => Math.round(n);

export function isCash(method: Tender): boolean {
  return method === StaffServiceAgent.PaymentTender.Cash;
}

/** Price of one ticket: the manager override if set, otherwise the list price of its category. */
export function ticketPrice(ticket: PosTicket, rows: readonly PriceRowLike[]): number {
  if (ticket.overridePrice !== undefined) {
    return ticket.overridePrice;
  }
  return rows.find(r => r.patronCategoryId === ticket.patronCategoryId)?.price ?? 0;
}

export function ticketsTotal(tickets: readonly PosTicket[], rows: readonly PriceRowLike[]): number {
  return tickets.reduce((sum, t) => sum + ticketPrice(t, rows), 0);
}

export function foodUnitPrice(line: PosFoodLine): number {
  return line.overridePrice ?? line.unitPrice;
}

export function foodsTotal(lines: readonly PosFoodLine[]): number {
  return lines.reduce((sum, l) => sum + foodUnitPrice(l) * l.quantity, 0);
}

/** Sum of tickets and food before any discount, points or gift card. */
export function selectionTotal(tickets: readonly PosTicket[], rows: readonly PriceRowLike[], foods: readonly PosFoodLine[]): number {
  return ticketsTotal(tickets, rows) + foodsTotal(foods);
}

/**
 * Works out whether the tenders cover `due`, how much change goes back and what is still wrong.
 * Change can only come out of cash: card and QR payments must not exceed the amount due.
 */
export function settle(due: number, tenders: readonly TenderEntry[]): Settlement {
  const amountDue = Math.max(0, round(due));
  const cash = tenders.filter(t => isCash(t.method)).reduce((s, t) => s + round(t.amount), 0);
  const nonCash = tenders.filter(t => !isCash(t.method)).reduce((s, t) => s + round(t.amount), 0);
  const tendered = cash + nonCash;
  const nonCashExcess = nonCash > amountDue;
  const missingReference = tenders.some(t => !isCash(t.method) && t.reference.trim() === '');
  const invalidAmount = tenders.some(t => !(round(t.amount) > 0));
  const remaining = Math.max(0, amountDue - tendered);
  const changeDue = nonCashExcess ? 0 : Math.max(0, tendered - amountDue);
  const complete = remaining === 0 && !nonCashExcess && !missingReference && !invalidAmount;
  return {
    due: amountDue, tendered, remaining, changeDue, nonCashExcess, missingReference, invalidAmount, complete,
    usesCash: cash > 0 || tenders.some(t => isCash(t.method)),
  };
}

/** The amount still to collect after the other tender lines, to prefill a line with. */
export function remainingAfter(due: number, tenders: readonly TenderEntry[], exceptIndex: number): number {
  const others = tenders.filter((_, i) => i !== exceptIndex).reduce((s, t) => s + round(t.amount), 0);
  return Math.max(0, round(due) - others);
}

/** Seat lines of the sale request: one per physical seat; a double ticket's override is split across its halves. */
export function seatItems(tickets: readonly PosTicket[]): { seatId: string; patronCategoryId: string; overrideUnitPrice?: number }[] {
  const items: { seatId: string; patronCategoryId: string; overrideUnitPrice?: number }[] = [];
  for (const ticket of tickets) {
    for (const seatId of ticket.seatIds) {
      items.push({
        seatId,
        patronCategoryId: ticket.patronCategoryId,
        overrideUnitPrice: ticket.overridePrice === undefined ? undefined : ticket.overridePrice / ticket.seatIds.length,
      });
    }
  }
  return items;
}

export function foodItems(lines: readonly PosFoodLine[]): { foodAndDrinkId: string; quantity: number; overrideUnitPrice?: number }[] {
  return lines
    .filter(l => l.quantity > 0)
    .map(l => ({ foodAndDrinkId: l.foodAndDrinkId, quantity: l.quantity, overrideUnitPrice: l.overridePrice }));
}

/** True when any ticket or food line carries a price override. */
export function hasOverride(tickets: readonly PosTicket[], foods: readonly PosFoodLine[]): boolean {
  return tickets.some(t => t.overridePrice !== undefined) || foods.some(f => f.overridePrice !== undefined);
}

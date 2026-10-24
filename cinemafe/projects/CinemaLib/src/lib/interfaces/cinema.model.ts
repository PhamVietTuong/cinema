import { CinemaServiceAgent } from '../services/cinema-http.service';

/** Display role shown in the admin user list (derived from UserDTO.userTypeName). */
export enum UserRole {
  Admin = 'Admin',
  Customer = 'Khách Hàng',
  TheaterStaff = 'Nhân viên rạp',
}

// NOTE: display-label lookups for NSwag-generated enums (like ProjectionForm below)
// belong in this file, not in individual feature components — keep them reusable.

/**
 * Display label for each ShowTime.ProjectionForm value. This axis is the image dimension only.
 * A room's class (IMAX, 4DX, Lagom…) is a separate axis carried by RoomType — the same IMAX hall
 * screens both IMAX 2D and IMAX 3D — so the two are composed for display by screeningFormatLabel.
 */
export const ProjectionFormValues: { value: CinemaServiceAgent.ProjectionForm; name: string }[] = [
  { value: CinemaServiceAgent.ProjectionForm.TwoD, name: '2D' },
  { value: CinemaServiceAgent.ProjectionForm.ThreeD, name: '3D' },
];

/** The label a customer sees for a screening: room class then dimension, e.g. "IMAX 2D". */
export function screeningFormatLabel(roomTypeName?: string, form?: CinemaServiceAgent.ProjectionForm): string {
  const dimension = ProjectionFormValues.find(x => x.value === form)?.name ?? '2D';
  return roomTypeName ? `${roomTypeName} ${dimension}` : dimension;
}

/** Display label + timetable CSS class for each ShowTime.ShowTimeType value. */
export const ShowTimeTypeValues: { value: CinemaServiceAgent.ShowTimeType; name: string; cls: string }[] = [
  { value: CinemaServiceAgent.ShowTimeType.Normal, name: 'Thường', cls: 'st-block--normal' },
  { value: CinemaServiceAgent.ShowTimeType.Premiere, name: 'Công Chiếu', cls: 'st-block--premiere' },
  { value: CinemaServiceAgent.ShowTimeType.Special, name: 'Đặc Biệt', cls: 'st-block--special' },
];

/**
 * i18n-key label for a seat's kind (Single/Double). Keyed by the boolean `isDouble` rather than
 * the generated SeatKind enum, since NSwag emits a separate SeatKind type per client namespace
 * (CinemaServiceAgent vs PaymentServiceAgent) — comparing by boolean sidesteps that mismatch.
 */
export const SeatKindValues: { isDouble: boolean; name: string }[] = [
  { isDouble: false, name: 'booking.seats.kindStandard' },
  { isDouble: true, name: 'booking.seats.kindDouble' },
];

export function seatKindLabel(isDouble?: boolean): string {
  return SeatKindValues.find(v => v.isDouble === !!isDouble)?.name ?? SeatKindValues[0].name;
}

/** i18n-key label for each Room.RoomStatus value. */
export const RoomStatusValues: { value: CinemaServiceAgent.RoomStatus; name: string }[] = [
  { value: CinemaServiceAgent.RoomStatus.Active, name: 'theaters.rooms.statusActive' },
  { value: CinemaServiceAgent.RoomStatus.Maintenance, name: 'theaters.rooms.statusMaintenance' },
  { value: CinemaServiceAgent.RoomStatus.Inactive, name: 'theaters.rooms.statusInactive' },
];

/** i18n-key label for each Template.Channel value. */
export const TemplateChannelValues: { value: CinemaServiceAgent.TemplateChannel; name: string }[] = [
  { value: CinemaServiceAgent.TemplateChannel.Email, name: 'templates.channelEmail' },
  { value: CinemaServiceAgent.TemplateChannel.Sms, name: 'templates.channelSms' },
];

/** i18n-key label for each MovieCredit.CreditType value. */
export const CreditTypeValues: { value: CinemaServiceAgent.CreditType; name: string }[] = [
  { value: CinemaServiceAgent.CreditType.Director, name: 'movies.form.creditDirector' },
  { value: CinemaServiceAgent.CreditType.Actor, name: 'movies.form.creditActor' },
];

/** i18n key for a CreditType value (falls back to Director). */
export function creditTypeLabel(c?: CinemaServiceAgent.CreditType): string {
  return CreditTypeValues.find(v => v.value === c)?.name ?? CreditTypeValues[0].name;
}

/** i18n-key label for each Invoice.InvoiceStatus value. */
export const InvoiceStatusValues = [
  { value: CinemaServiceAgent.InvoiceStatus.Pending, name: 'invoices.statusPending' },
  { value: CinemaServiceAgent.InvoiceStatus.Paid, name: 'invoices.statusPaid' },
  { value: CinemaServiceAgent.InvoiceStatus.Cancelled, name: 'invoices.statusCancelled' },
  { value: CinemaServiceAgent.InvoiceStatus.Failed, name: 'invoices.statusFailed' },
];

/** CSS pill class for each Invoice.InvoiceStatus value (used by the admin invoices grid). */
export function invoiceStatusPillClass(s?: CinemaServiceAgent.InvoiceStatus): string {
  switch (s) {
    case CinemaServiceAgent.InvoiceStatus.Paid: return 'ad-pill--success';
    case CinemaServiceAgent.InvoiceStatus.Pending: return 'ad-pill--warn';
    default: return 'ad-pill--danger';
  }
}

/** What `cl-status-pill` can render; each kind maps its `value` to a label key and pill class in this file. */
export type StatusPillKind = 'invoice' | 'scanOutcome' | 'auditAction' | 'incident' | 'incidentSeverity' | 'roomStatus' | 'foodOrder';

/** i18n label key (under `kitchen.status`) and pill class for each FoodOrderStatus. */
export const FoodOrderStatusSpecs: Record<CinemaServiceAgent.FoodOrderStatus, { labelKey: string; cssClass: string }> = {
  [CinemaServiceAgent.FoodOrderStatus.None]: { labelKey: 'kitchen.status.none', cssClass: 'ad-pill--neutral' },
  [CinemaServiceAgent.FoodOrderStatus.Pending]: { labelKey: 'kitchen.status.pending', cssClass: 'ad-pill--warn' },
  [CinemaServiceAgent.FoodOrderStatus.Preparing]: { labelKey: 'kitchen.status.preparing', cssClass: 'ad-pill--violet' },
  [CinemaServiceAgent.FoodOrderStatus.Ready]: { labelKey: 'kitchen.status.ready', cssClass: 'ad-pill--success' },
  [CinemaServiceAgent.FoodOrderStatus.HandedOver]: { labelKey: 'kitchen.status.handedOver', cssClass: 'ad-pill--neutral' },
  [CinemaServiceAgent.FoodOrderStatus.Cancelled]: { labelKey: 'kitchen.status.cancelled', cssClass: 'ad-pill--danger' },
};

/** Label key and pill class of a food order status; an unknown value reads as "none". */
export function foodOrderStatusSpec(status?: CinemaServiceAgent.FoodOrderStatus): { labelKey: string; cssClass: string } {
  return FoodOrderStatusSpecs[status as CinemaServiceAgent.FoodOrderStatus] ?? FoodOrderStatusSpecs[CinemaServiceAgent.FoodOrderStatus.None];
}

/** i18n label key (under `salesReports.groupBy`) for each SalesGroupBy value, in display order. */
export const SalesGroupByValues: { value: CinemaServiceAgent.SalesGroupBy; name: string }[] = [
  { value: CinemaServiceAgent.SalesGroupBy.Day, name: 'salesReports.groupBy.day' },
  { value: CinemaServiceAgent.SalesGroupBy.Movie, name: 'salesReports.groupBy.movie' },
  { value: CinemaServiceAgent.SalesGroupBy.Theater, name: 'salesReports.groupBy.theater' },
  { value: CinemaServiceAgent.SalesGroupBy.PaymentMethod, name: 'salesReports.groupBy.paymentMethod' },
  { value: CinemaServiceAgent.SalesGroupBy.Staff, name: 'salesReports.groupBy.staff' },
  { value: CinemaServiceAgent.SalesGroupBy.Channel, name: 'salesReports.groupBy.channel' },
];

/** i18n label key for a SalesGroupBy value. */
export function salesGroupByLabel(groupBy?: CinemaServiceAgent.SalesGroupBy): string {
  return SalesGroupByValues.find(v => v.value === groupBy)?.name ?? SalesGroupByValues[0].name;
}

/** Traffic-light tone of a gate scan result: green admits, amber needs the gate keeper's judgement, red refuses. */
export type ScanTone = 'success' | 'warn' | 'danger';

/** i18n label key (under `gate.outcome`) and tone for each gate ScanOutcome. */
export const ScanOutcomeSpecs: Record<CinemaServiceAgent.ScanOutcome, { labelKey: string; tone: ScanTone }> = {
  [CinemaServiceAgent.ScanOutcome.Admitted]: { labelKey: 'gate.outcome.admitted', tone: 'success' },
  [CinemaServiceAgent.ScanOutcome.NotFound]: { labelKey: 'gate.outcome.notFound', tone: 'danger' },
  [CinemaServiceAgent.ScanOutcome.NotPaid]: { labelKey: 'gate.outcome.notPaid', tone: 'danger' },
  [CinemaServiceAgent.ScanOutcome.AlreadyUsed]: { labelKey: 'gate.outcome.alreadyUsed', tone: 'danger' },
  [CinemaServiceAgent.ScanOutcome.WrongTheater]: { labelKey: 'gate.outcome.wrongTheater', tone: 'danger' },
  [CinemaServiceAgent.ScanOutcome.WrongShowTime]: { labelKey: 'gate.outcome.wrongShowTime', tone: 'warn' },
  [CinemaServiceAgent.ScanOutcome.TooEarly]: { labelKey: 'gate.outcome.tooEarly', tone: 'warn' },
  [CinemaServiceAgent.ScanOutcome.Expired]: { labelKey: 'gate.outcome.expired', tone: 'danger' },
  [CinemaServiceAgent.ScanOutcome.AgeCheckRequired]: { labelKey: 'gate.outcome.ageCheckRequired', tone: 'warn' },
};

/** Label key and tone of a scan outcome; an unknown value is treated as a refusal. */
export function scanOutcomeSpec(outcome?: CinemaServiceAgent.ScanOutcome): { labelKey: string; tone: ScanTone } {
  return ScanOutcomeSpecs[outcome as CinemaServiceAgent.ScanOutcome] ?? ScanOutcomeSpecs[CinemaServiceAgent.ScanOutcome.NotFound];
}

/** CSS pill class for a scan tone. */
export function scanToneCssClass(tone: ScanTone): string {
  return 'ad-pill--' + tone;
}

/** i18n label key (under `auditLog.action`) for each AuditAction value. */
export const AuditActionValues: { value: CinemaServiceAgent.AuditAction; name: string }[] = [
  { value: CinemaServiceAgent.AuditAction.Other, name: 'auditLog.action.other' },
  { value: CinemaServiceAgent.AuditAction.OverrideFailed, name: 'auditLog.action.overrideFailed' },
  { value: CinemaServiceAgent.AuditAction.OverridePinChanged, name: 'auditLog.action.overridePinChanged' },
  { value: CinemaServiceAgent.AuditAction.PriceOverride, name: 'auditLog.action.priceOverride' },
  { value: CinemaServiceAgent.AuditAction.Exchange, name: 'auditLog.action.exchange' },
  { value: CinemaServiceAgent.AuditAction.Reprint, name: 'auditLog.action.reprint' },
  { value: CinemaServiceAgent.AuditAction.VoidSale, name: 'auditLog.action.voidSale' },
  { value: CinemaServiceAgent.AuditAction.CashPayOut, name: 'auditLog.action.cashPayOut' },
  { value: CinemaServiceAgent.AuditAction.DrawerReconcile, name: 'auditLog.action.drawerReconcile' },
  { value: CinemaServiceAgent.AuditAction.Compensation, name: 'auditLog.action.compensation' },
  { value: CinemaServiceAgent.AuditAction.PointsAdjust, name: 'auditLog.action.pointsAdjust' },
  { value: CinemaServiceAgent.AuditAction.ResendTicket, name: 'auditLog.action.resendTicket' },
  { value: CinemaServiceAgent.AuditAction.BlockSeat, name: 'auditLog.action.blockSeat' },
  { value: CinemaServiceAgent.AuditAction.BlockRoom, name: 'auditLog.action.blockRoom' },
  { value: CinemaServiceAgent.AuditAction.TicketAdmitOverride, name: 'auditLog.action.ticketAdmitOverride' },
  { value: CinemaServiceAgent.AuditAction.GateScan, name: 'auditLog.action.gateScan' },
];

/** i18n label key for an AuditAction value. */
export function auditActionLabel(action?: CinemaServiceAgent.AuditAction): string {
  return AuditActionValues.find(v => v.value === action)?.name ?? AuditActionValues[0].name;
}

/** CSS pill class for an AuditAction: failures red, money-affecting actions amber, the rest neutral. */
export function auditActionPillClass(action?: CinemaServiceAgent.AuditAction): string {
  switch (action) {
    case CinemaServiceAgent.AuditAction.OverrideFailed: return 'ad-pill--danger';
    case CinemaServiceAgent.AuditAction.PriceOverride:
    case CinemaServiceAgent.AuditAction.VoidSale:
    case CinemaServiceAgent.AuditAction.CashPayOut:
    case CinemaServiceAgent.AuditAction.Compensation:
    case CinemaServiceAgent.AuditAction.PointsAdjust:
    case CinemaServiceAgent.AuditAction.TicketAdmitOverride: return 'ad-pill--warn';
    default: return 'ad-pill--neutral';
  }
}

// ── Operations and workforce (staff app) ─────────────────────────────────────────────────────────────

/** i18n-key label for each Operations IncidentCategory value. */
export const IncidentCategoryValues: { value: CinemaServiceAgent.IncidentCategory; name: string }[] = [
  { value: CinemaServiceAgent.IncidentCategory.Other, name: 'staffEnums.incidentCategory.other' },
  { value: CinemaServiceAgent.IncidentCategory.Seat, name: 'staffEnums.incidentCategory.seat' },
  { value: CinemaServiceAgent.IncidentCategory.Room, name: 'staffEnums.incidentCategory.room' },
  { value: CinemaServiceAgent.IncidentCategory.Projection, name: 'staffEnums.incidentCategory.projection' },
  { value: CinemaServiceAgent.IncidentCategory.Sound, name: 'staffEnums.incidentCategory.sound' },
  { value: CinemaServiceAgent.IncidentCategory.Safety, name: 'staffEnums.incidentCategory.safety' },
  { value: CinemaServiceAgent.IncidentCategory.Customer, name: 'staffEnums.incidentCategory.customer' },
  { value: CinemaServiceAgent.IncidentCategory.Cleanliness, name: 'staffEnums.incidentCategory.cleanliness' },
];

/** i18n key for an incident category (falls back to Other). */
export function incidentCategoryLabel(c?: CinemaServiceAgent.IncidentCategory): string {
  return IncidentCategoryValues.find(v => v.value === c)?.name ?? IncidentCategoryValues[0].name;
}

/** i18n-key label for each IncidentSeverity value. */
export const IncidentSeverityValues: { value: CinemaServiceAgent.IncidentSeverity; name: string }[] = [
  { value: CinemaServiceAgent.IncidentSeverity.Low, name: 'staffEnums.incidentSeverity.low' },
  { value: CinemaServiceAgent.IncidentSeverity.Medium, name: 'staffEnums.incidentSeverity.medium' },
  { value: CinemaServiceAgent.IncidentSeverity.High, name: 'staffEnums.incidentSeverity.high' },
  { value: CinemaServiceAgent.IncidentSeverity.Critical, name: 'staffEnums.incidentSeverity.critical' },
];

/** i18n key for an incident severity (falls back to Low). */
export function incidentSeverityLabel(s?: CinemaServiceAgent.IncidentSeverity): string {
  return IncidentSeverityValues.find(v => v.value === s)?.name ?? IncidentSeverityValues[0].name;
}

/** CSS pill class for each IncidentSeverity value. */
export function incidentSeverityPillClass(s?: CinemaServiceAgent.IncidentSeverity): string {
  switch (s) {
    case CinemaServiceAgent.IncidentSeverity.Critical: return 'ad-pill--danger';
    case CinemaServiceAgent.IncidentSeverity.High: return 'ad-pill--warn';
    case CinemaServiceAgent.IncidentSeverity.Medium: return 'ad-pill--violet';
    default: return 'ad-pill--neutral';
  }
}

/** i18n-key label for each IncidentStatus value. */
export const IncidentStatusValues: { value: CinemaServiceAgent.IncidentStatus; name: string }[] = [
  { value: CinemaServiceAgent.IncidentStatus.Open, name: 'staffEnums.incidentStatus.open' },
  { value: CinemaServiceAgent.IncidentStatus.Resolved, name: 'staffEnums.incidentStatus.resolved' },
];

/** i18n key for an incident status (falls back to Open). */
export function incidentStatusLabel(s?: CinemaServiceAgent.IncidentStatus): string {
  return IncidentStatusValues.find(v => v.value === s)?.name ?? IncidentStatusValues[0].name;
}

/** CSS pill class for each IncidentStatus value. */
export function incidentStatusPillClass(s?: CinemaServiceAgent.IncidentStatus): string {
  return s === CinemaServiceAgent.IncidentStatus.Resolved ? 'ad-pill--success' : 'ad-pill--warn';
}

/** i18n key for a room status shown on the staff schedule board (the enum values match the Cinema API's RoomStatus). */
export function roomStatusLabel(s?: number): string {
  switch (s) {
    case 1: return 'staffEnums.roomStatus.maintenance';
    case 2: return 'staffEnums.roomStatus.inactive';
    default: return 'staffEnums.roomStatus.active';
  }
}

/** CSS pill class for a room status (0 Active, 1 Maintenance, 2 Inactive). */
export function roomStatusPillClass(s?: number): string {
  switch (s) {
    case 1: return 'ad-pill--warn';
    case 2: return 'ad-pill--neutral';
    default: return 'ad-pill--success';
  }
}

/** Label (i18n key) and CSS class a `cl-status-pill` shows for a kind/value pair. */
export function statusPillSpec(kind: StatusPillKind, value: unknown): { labelKey: string; cssClass: string } {
  switch (kind) {
    case 'invoice': {
      const status = value as CinemaServiceAgent.InvoiceStatus;
      return {
        labelKey: InvoiceStatusValues.find(v => v.value === status)?.name ?? InvoiceStatusValues[0].name,
        cssClass: invoiceStatusPillClass(status),
      };
    }
    case 'scanOutcome': {
      const spec = scanOutcomeSpec(value as CinemaServiceAgent.ScanOutcome);
      return { labelKey: spec.labelKey, cssClass: scanToneCssClass(spec.tone) };
    }
    case 'auditAction': {
      const action = value as CinemaServiceAgent.AuditAction;
      return { labelKey: auditActionLabel(action), cssClass: auditActionPillClass(action) };
    }
    case 'foodOrder': {
      return foodOrderStatusSpec(value as CinemaServiceAgent.FoodOrderStatus);
    }
    case 'incident': {
      const status = value as CinemaServiceAgent.IncidentStatus;
      return { labelKey: incidentStatusLabel(status), cssClass: incidentStatusPillClass(status) };
    }
    case 'incidentSeverity': {
      const severity = value as CinemaServiceAgent.IncidentSeverity;
      return { labelKey: incidentSeverityLabel(severity), cssClass: incidentSeverityPillClass(severity) };
    }
    case 'roomStatus': {
      const status = value as number;
      return { labelKey: roomStatusLabel(status), cssClass: roomStatusPillClass(status) };
    }
  }
}


/** i18n-key label for each counter payment tender a cashier can take. */
export const CounterTenderValues: { value: CinemaServiceAgent.PaymentTender; name: string }[] = [
  { value: CinemaServiceAgent.PaymentTender.Cash, name: 'pos.tender.cash' },
  { value: CinemaServiceAgent.PaymentTender.Card, name: 'pos.tender.card' },
  { value: CinemaServiceAgent.PaymentTender.QrWallet, name: 'pos.tender.qrWallet' },
];

/**
 * Max orderable quantity of a food or combo: 0 when sold out, the public availability cap when stock is
 * tracked, null when unlimited. Shared by the customer booking page and the counter POS.
 */
export function foodOrderCap(f: { isOutOfStock?: boolean; availableQuantity?: number | null }): number | null {
  if (f.isOutOfStock) {
    return 0;
  }
  if (f.availableQuantity === null || f.availableQuantity === undefined) {
    return null;
  }
  return Math.max(0, f.availableQuantity);
}

/** i18n-key label (full + short) for each TicketPrice.DayOfWeek value, ordered Mon→Sun for display. */
export const DayOfWeekValues: { value: CinemaServiceAgent.DayOfWeek; name: string; shortName: string }[] = [
  { value: CinemaServiceAgent.DayOfWeek.Monday, name: 'common.days.mon', shortName: 'common.days.monShort' },
  { value: CinemaServiceAgent.DayOfWeek.Tuesday, name: 'common.days.tue', shortName: 'common.days.tueShort' },
  { value: CinemaServiceAgent.DayOfWeek.Wednesday, name: 'common.days.wed', shortName: 'common.days.wedShort' },
  { value: CinemaServiceAgent.DayOfWeek.Thursday, name: 'common.days.thu', shortName: 'common.days.thuShort' },
  { value: CinemaServiceAgent.DayOfWeek.Friday, name: 'common.days.fri', shortName: 'common.days.friShort' },
  { value: CinemaServiceAgent.DayOfWeek.Saturday, name: 'common.days.sat', shortName: 'common.days.satShort' },
  { value: CinemaServiceAgent.DayOfWeek.Sunday, name: 'common.days.sun', shortName: 'common.days.sunShort' },
];

/**
 * Visual band for a TicketPrice/Holiday multiplier, driving the weekly pricing timeline's segment
 * colors. Purely a display derivation from the numeric value — nothing is stored. The 'holiday' band
 * is never derived from the number; the caller picks it explicitly when the price comes from a Holiday
 * override rather than the weekday matrix.
 */
export type MultiplierBand = 'low' | 'standard' | 'peak' | 'holiday';

export function multiplierBand(multiplier: number): Exclude<MultiplierBand, 'holiday'> {
  if (multiplier < 1) {
    return 'low';
  }
  if (multiplier < 1.2) {
    return 'standard';
  }
  return 'peak';
}

export const MultiplierBandSpecs: Record<MultiplierBand, { labelKey: string; cssClass: string }> = {
  low: { labelKey: 'theaters.ticketPrices.bands.low', cssClass: 'tp-band--low' },
  standard: { labelKey: 'theaters.ticketPrices.bands.standard', cssClass: 'tp-band--standard' },
  peak: { labelKey: 'theaters.ticketPrices.bands.peak', cssClass: 'tp-band--peak' },
  holiday: { labelKey: 'theaters.ticketPrices.bands.holiday', cssClass: 'tp-band--holiday' },
};

/** Display label for each PromotionProgram.Mechanic value (how the program affects price). */
export const PromotionMechanicValues: { value: CinemaServiceAgent.PromotionMechanic; name: string }[] = [
  { value: CinemaServiceAgent.PromotionMechanic.Info, name: 'promoProgram.mechanic.info' },
  { value: CinemaServiceAgent.PromotionMechanic.FixedPrice, name: 'promoProgram.mechanic.fixedPrice' },
  { value: CinemaServiceAgent.PromotionMechanic.PercentOff, name: 'promoProgram.mechanic.percentOff' },
];

/** Display label for each PromotionProgram.Eligibility value (who qualifies). */
export const PromotionEligibilityValues: { value: CinemaServiceAgent.PromotionEligibility; name: string }[] = [
  { value: CinemaServiceAgent.PromotionEligibility.Everyone, name: 'promoProgram.eligibility.everyone' },
  { value: CinemaServiceAgent.PromotionEligibility.Members, name: 'promoProgram.eligibility.members' },
  { value: CinemaServiceAgent.PromotionEligibility.StudentOrTeacher, name: 'promoProgram.eligibility.studentOrTeacher' },
  { value: CinemaServiceAgent.PromotionEligibility.AgeLimit, name: 'promoProgram.eligibility.ageLimit' },
];

/** Display label for each SalesChannel value, used by PromotionProgram.AllowedChannelsMask. */
export const SalesChannelValues: { value: CinemaServiceAgent.SalesChannel; name: string }[] = [
  { value: CinemaServiceAgent.SalesChannel.Online, name: 'promoProgram.channel.online' },
  { value: CinemaServiceAgent.SalesChannel.Counter, name: 'promoProgram.channel.counter' },
];

/** Decodes a DaysOfWeekMask bitmask (bit n = CinemaServiceAgent.DayOfWeek value n) into a day list. */
export function daysMaskToList(mask?: number | null): CinemaServiceAgent.DayOfWeek[] {
  if (!mask) {
    return [];
  }
  return DayOfWeekValues.map(d => d.value).filter(d => (mask & (1 << d)) !== 0);
}

/** Encodes a day list back into a DaysOfWeekMask bitmask; an empty list means "any day" (null). */
export function daysListToMask(days: CinemaServiceAgent.DayOfWeek[]): number | null {
  return days.length ? days.reduce((mask, d) => mask | (1 << d), 0) : null;
}

/** Decodes an AllowedChannelsMask bitmask into a channel list. */
export function channelsMaskToList(mask?: number | null): CinemaServiceAgent.SalesChannel[] {
  if (!mask) {
    return [];
  }
  return SalesChannelValues.map(c => c.value).filter(c => (mask & (1 << c)) !== 0);
}

/** Encodes a channel list back into an AllowedChannelsMask bitmask; empty means "all channels" (null). */
export function channelsListToMask(channels: CinemaServiceAgent.SalesChannel[]): number | null {
  return channels.length ? channels.reduce((mask, c) => mask | (1 << c), 0) : null;
}

/** Rotten Tomatoes' own "fresh" cut-off: scores at or above this percentage are fresh, below is rotten. */
const ROTTEN_TOMATOES_FRESH_THRESHOLD = 60;

export type RottenTomatoesTier = 'fresh' | 'rotten';

/** Classifies a Rotten Tomatoes score string (e.g. "93%") as fresh/rotten; null when unparsable. */
export function rottenTomatoesTier(score?: string | null): RottenTomatoesTier | null {
  if (!score) {
    return null;
  }
  const match = score.match(/\d+/);
  if (!match) {
    return null;
  }
  return Number(match[0]) >= ROTTEN_TOMATOES_FRESH_THRESHOLD ? 'fresh' : 'rotten';
}
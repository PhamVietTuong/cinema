import { CinemaServiceAgent } from '../services/cinema-http.service';
import { StaffServiceAgent } from '../services/staff-http.service';

/** Display role shown in the admin user list (derived from UserDTO.userTypeName). */
export enum UserRole {
  Admin = 'Admin',
  Customer = 'Khách Hàng',
  TheaterStaff = 'Nhân viên rạp',
  TheaterManager = 'Quản lý rạp',
  BoxOfficeStaff = 'Nhân viên bán vé',
  GateStaff = 'Nhân viên soát vé',
  KitchenStaff = 'Nhân viên bếp',
  RegionalManager = 'Quản lý vùng',
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

/** i18n-key label for each Invoice.InvoiceStatus value. */
export const InvoiceStatusValues = [
  { value: CinemaServiceAgent.InvoiceStatus.Pending, name: 'invoices.statusPending' },
  { value: CinemaServiceAgent.InvoiceStatus.Paid, name: 'invoices.statusPaid' },
  { value: CinemaServiceAgent.InvoiceStatus.Cancelled, name: 'invoices.statusCancelled' },
  { value: CinemaServiceAgent.InvoiceStatus.Failed, name: 'invoices.statusFailed' },
  { value: CinemaServiceAgent.InvoiceStatus.Refunded, name: 'invoices.statusRefunded' },
];

/** i18n-key label for each StockMovement.StockMovementType value. */
export const StockMovementTypeValues: { value: CinemaServiceAgent.StockMovementType; name: string }[] = [
  { value: CinemaServiceAgent.StockMovementType.Receive, name: 'warehouse.movementType.receive' },
  { value: CinemaServiceAgent.StockMovementType.Sale, name: 'warehouse.movementType.sale' },
  { value: CinemaServiceAgent.StockMovementType.SaleReversal, name: 'warehouse.movementType.saleReversal' },
  { value: CinemaServiceAgent.StockMovementType.Adjust, name: 'warehouse.movementType.adjust' },
  { value: CinemaServiceAgent.StockMovementType.Waste, name: 'warehouse.movementType.waste' },
];

/** i18n-key label for each StockMovement.StockReasonCode value. */
export const StockReasonCodeValues: { value: CinemaServiceAgent.StockReasonCode; name: string }[] = [
  { value: CinemaServiceAgent.StockReasonCode.Other, name: 'warehouse.reason.other' },
  { value: CinemaServiceAgent.StockReasonCode.Expired, name: 'warehouse.reason.expired' },
  { value: CinemaServiceAgent.StockReasonCode.Damaged, name: 'warehouse.reason.damaged' },
  { value: CinemaServiceAgent.StockReasonCode.Spilled, name: 'warehouse.reason.spilled' },
  { value: CinemaServiceAgent.StockReasonCode.TheftOrLoss, name: 'warehouse.reason.theftOrLoss' },
  { value: CinemaServiceAgent.StockReasonCode.StockCountCorrection, name: 'warehouse.reason.stockCountCorrection' },
  { value: CinemaServiceAgent.StockReasonCode.OpeningBalance, name: 'warehouse.reason.openingBalance' },
];

/** i18n-key label for each StaffReasonCode (refund / exchange / compensation reasons). */
export const StaffReasonCodeValues: { value: StaffServiceAgent.StaffReasonCode; name: string }[] = [
  { value: StaffServiceAgent.StaffReasonCode.CustomerRequest, name: 'staffReason.customerRequest' },
  { value: StaffServiceAgent.StaffReasonCode.WrongShowtime, name: 'staffReason.wrongShowtime' },
  { value: StaffServiceAgent.StaffReasonCode.DuplicateSale, name: 'staffReason.duplicateSale' },
  { value: StaffServiceAgent.StaffReasonCode.ServiceFailure, name: 'staffReason.serviceFailure' },
  { value: StaffServiceAgent.StaffReasonCode.TechnicalIssue, name: 'staffReason.technicalIssue' },
  { value: StaffServiceAgent.StaffReasonCode.PriceMatch, name: 'staffReason.priceMatch' },
  { value: StaffServiceAgent.StaffReasonCode.Compensation, name: 'staffReason.compensation' },
  { value: StaffServiceAgent.StaffReasonCode.Other, name: 'staffReason.other' },
];

/** i18n-key label for each PaymentTender. */
export const PaymentTenderValues: { value: StaffServiceAgent.PaymentTender; name: string }[] = [
  { value: StaffServiceAgent.PaymentTender.Cash, name: 'tender.cash' },
  { value: StaffServiceAgent.PaymentTender.Card, name: 'tender.card' },
  { value: StaffServiceAgent.PaymentTender.QrWallet, name: 'tender.qrWallet' },
  { value: StaffServiceAgent.PaymentTender.GiftCard, name: 'tender.giftCard' },
  { value: StaffServiceAgent.PaymentTender.Points, name: 'tender.points' },
  { value: StaffServiceAgent.PaymentTender.Online, name: 'tender.online' },
];

/** i18n key of a PaymentTender (falls back to the first entry for an unknown value). */
export function paymentTenderLabel(tender?: StaffServiceAgent.PaymentTender): string {
  return PaymentTenderValues.find(v => v.value === tender)?.name ?? PaymentTenderValues[0].name;
}

/** Tenders a refund can be paid back through at the counter. */
export const RefundTenderValues: { value: StaffServiceAgent.PaymentTender; name: string }[] = PaymentTenderValues
  .filter(v => v.value === StaffServiceAgent.PaymentTender.Cash
    || v.value === StaffServiceAgent.PaymentTender.Card
    || v.value === StaffServiceAgent.PaymentTender.QrWallet);

/** i18n-key label for each CashDrawerStatus. */
export const CashDrawerStatusValues: { value: StaffServiceAgent.CashDrawerStatus; name: string }[] = [
  { value: StaffServiceAgent.CashDrawerStatus.Open, name: 'drawerStatus.open' },
  { value: StaffServiceAgent.CashDrawerStatus.Closed, name: 'drawerStatus.closed' },
  { value: StaffServiceAgent.CashDrawerStatus.Reconciled, name: 'drawerStatus.reconciled' },
];

/** CSS pill class for a CashDrawerStatus: open amber, closed (awaiting reconciliation) red, reconciled green. */
export function cashDrawerStatusPillClass(s?: StaffServiceAgent.CashDrawerStatus): string {
  switch (s) {
    case StaffServiceAgent.CashDrawerStatus.Reconciled: return 'ad-pill--success';
    case StaffServiceAgent.CashDrawerStatus.Open: return 'ad-pill--warn';
    default: return 'ad-pill--danger';
  }
}

/** i18n-key label for each StoragePlan.StoragePlanStatus value. */
export const StoragePlanStatusValues: { value: CinemaServiceAgent.StoragePlanStatus; name: string }[] = [
  { value: CinemaServiceAgent.StoragePlanStatus.Draft, name: 'warehouse.planStatus.draft' },
  { value: CinemaServiceAgent.StoragePlanStatus.Submitted, name: 'warehouse.planStatus.submitted' },
  { value: CinemaServiceAgent.StoragePlanStatus.Approved, name: 'warehouse.planStatus.approved' },
  { value: CinemaServiceAgent.StoragePlanStatus.Rejected, name: 'warehouse.planStatus.rejected' },
  { value: CinemaServiceAgent.StoragePlanStatus.Received, name: 'warehouse.planStatus.received' },
  { value: CinemaServiceAgent.StoragePlanStatus.Cancelled, name: 'warehouse.planStatus.cancelled' },
];

/** i18n key for a storage plan status (falls back to Draft's key). */
export function storagePlanStatusLabel(s?: CinemaServiceAgent.StoragePlanStatus): string {
  return StoragePlanStatusValues.find(v => v.value === s)?.name ?? StoragePlanStatusValues[0].name;
}

/** CSS pill class for each StoragePlanStatus value (same pill palette as invoices). */
export function storagePlanStatusPillClass(s?: CinemaServiceAgent.StoragePlanStatus): string {
  switch (s) {
    case CinemaServiceAgent.StoragePlanStatus.Approved:
    case CinemaServiceAgent.StoragePlanStatus.Received: return 'ad-pill--success';
    case CinemaServiceAgent.StoragePlanStatus.Submitted: return 'ad-pill--warn';
    case CinemaServiceAgent.StoragePlanStatus.Rejected: return 'ad-pill--danger';
    default: return 'ad-pill--neutral';
  }
}

/** CSS pill class for each Invoice.InvoiceStatus value (used by the admin invoices grid). */
export function invoiceStatusPillClass(s?: CinemaServiceAgent.InvoiceStatus): string {
  switch (s) {
    case CinemaServiceAgent.InvoiceStatus.Paid: return 'ad-pill--success';
    case CinemaServiceAgent.InvoiceStatus.Pending: return 'ad-pill--warn';
    case CinemaServiceAgent.InvoiceStatus.Refunded: return 'ad-pill--neutral';
    default: return 'ad-pill--danger';
  }
}

/** Derived stock health of an inventory row, driving its pill. */
export type StockLevel = 'untracked' | 'outOfStock' | 'low' | 'ok';

/** Resolves an inventory row (InventoryItemDTO) to its stock level. */
export function stockLevelOf(row: { trackInventory?: boolean; isOutOfStock?: boolean; isLowStock?: boolean }): StockLevel {
  if (!row.trackInventory) {
    return 'untracked';
  }
  if (row.isOutOfStock) {
    return 'outOfStock';
  }
  if (row.isLowStock) {
    return 'low';
  }
  return 'ok';
}

/** i18n label and pill class for each StockLevel. */
export const StockLevelPills: Record<StockLevel, { labelKey: string; cssClass: string }> = {
  untracked: { labelKey: 'inventory.status.untracked', cssClass: 'ad-pill--neutral' },
  outOfStock: { labelKey: 'inventory.status.outOfStock', cssClass: 'ad-pill--danger' },
  low: { labelKey: 'inventory.status.low', cssClass: 'ad-pill--warn' },
  ok: { labelKey: 'inventory.status.ok', cssClass: 'ad-pill--success' },
};

/** What `cl-status-pill` can render; each kind maps its `value` to a label key and pill class in this file. */
export type StatusPillKind = 'invoice' | 'storagePlan' | 'stockLevel' | 'scanOutcome' | 'auditAction' | 'incident' | 'incidentSeverity' | 'staffTask' | 'roomStatus' | 'foodOrder' | 'drawerStatus';

/** i18n label key (under `kitchen.status`) and pill class for each FoodOrderStatus. */
export const FoodOrderStatusSpecs: Record<StaffServiceAgent.FoodOrderStatus, { labelKey: string; cssClass: string }> = {
  [StaffServiceAgent.FoodOrderStatus.None]: { labelKey: 'kitchen.status.none', cssClass: 'ad-pill--neutral' },
  [StaffServiceAgent.FoodOrderStatus.Pending]: { labelKey: 'kitchen.status.pending', cssClass: 'ad-pill--warn' },
  [StaffServiceAgent.FoodOrderStatus.Preparing]: { labelKey: 'kitchen.status.preparing', cssClass: 'ad-pill--violet' },
  [StaffServiceAgent.FoodOrderStatus.Ready]: { labelKey: 'kitchen.status.ready', cssClass: 'ad-pill--success' },
  [StaffServiceAgent.FoodOrderStatus.HandedOver]: { labelKey: 'kitchen.status.handedOver', cssClass: 'ad-pill--neutral' },
  [StaffServiceAgent.FoodOrderStatus.Cancelled]: { labelKey: 'kitchen.status.cancelled', cssClass: 'ad-pill--danger' },
};

/** Label key and pill class of a food order status; an unknown value reads as "none". */
export function foodOrderStatusSpec(status?: StaffServiceAgent.FoodOrderStatus): { labelKey: string; cssClass: string } {
  return FoodOrderStatusSpecs[status as StaffServiceAgent.FoodOrderStatus] ?? FoodOrderStatusSpecs[StaffServiceAgent.FoodOrderStatus.None];
}

/** i18n label key (under `salesReports.groupBy`) for each SalesGroupBy value, in display order. */
export const SalesGroupByValues: { value: StaffServiceAgent.SalesGroupBy; name: string }[] = [
  { value: StaffServiceAgent.SalesGroupBy.Day, name: 'salesReports.groupBy.day' },
  { value: StaffServiceAgent.SalesGroupBy.Movie, name: 'salesReports.groupBy.movie' },
  { value: StaffServiceAgent.SalesGroupBy.Theater, name: 'salesReports.groupBy.theater' },
  { value: StaffServiceAgent.SalesGroupBy.PaymentMethod, name: 'salesReports.groupBy.paymentMethod' },
  { value: StaffServiceAgent.SalesGroupBy.Staff, name: 'salesReports.groupBy.staff' },
  { value: StaffServiceAgent.SalesGroupBy.Channel, name: 'salesReports.groupBy.channel' },
];

/** i18n label key for a SalesGroupBy value. */
export function salesGroupByLabel(groupBy?: StaffServiceAgent.SalesGroupBy): string {
  return SalesGroupByValues.find(v => v.value === groupBy)?.name ?? SalesGroupByValues[0].name;
}

/** Traffic-light tone of a gate scan result: green admits, amber needs the gate keeper's judgement, red refuses. */
export type ScanTone = 'success' | 'warn' | 'danger';

/** i18n label key (under `gate.outcome`) and tone for each gate ScanOutcome. */
export const ScanOutcomeSpecs: Record<StaffServiceAgent.ScanOutcome, { labelKey: string; tone: ScanTone }> = {
  [StaffServiceAgent.ScanOutcome.Admitted]: { labelKey: 'gate.outcome.admitted', tone: 'success' },
  [StaffServiceAgent.ScanOutcome.NotFound]: { labelKey: 'gate.outcome.notFound', tone: 'danger' },
  [StaffServiceAgent.ScanOutcome.NotPaid]: { labelKey: 'gate.outcome.notPaid', tone: 'danger' },
  [StaffServiceAgent.ScanOutcome.AlreadyUsed]: { labelKey: 'gate.outcome.alreadyUsed', tone: 'danger' },
  [StaffServiceAgent.ScanOutcome.WrongTheater]: { labelKey: 'gate.outcome.wrongTheater', tone: 'danger' },
  [StaffServiceAgent.ScanOutcome.WrongShowTime]: { labelKey: 'gate.outcome.wrongShowTime', tone: 'warn' },
  [StaffServiceAgent.ScanOutcome.TooEarly]: { labelKey: 'gate.outcome.tooEarly', tone: 'warn' },
  [StaffServiceAgent.ScanOutcome.Expired]: { labelKey: 'gate.outcome.expired', tone: 'danger' },
  [StaffServiceAgent.ScanOutcome.AgeCheckRequired]: { labelKey: 'gate.outcome.ageCheckRequired', tone: 'warn' },
};

/** Label key and tone of a scan outcome; an unknown value is treated as a refusal. */
export function scanOutcomeSpec(outcome?: StaffServiceAgent.ScanOutcome): { labelKey: string; tone: ScanTone } {
  return ScanOutcomeSpecs[outcome as StaffServiceAgent.ScanOutcome] ?? ScanOutcomeSpecs[StaffServiceAgent.ScanOutcome.NotFound];
}

/** CSS pill class for a scan tone. */
export function scanToneCssClass(tone: ScanTone): string {
  return 'ad-pill--' + tone;
}

/** i18n label key (under `auditLog.action`) for each AuditAction value. */
export const AuditActionValues: { value: StaffServiceAgent.AuditAction; name: string }[] = [
  { value: StaffServiceAgent.AuditAction.Other, name: 'auditLog.action.other' },
  { value: StaffServiceAgent.AuditAction.OverrideFailed, name: 'auditLog.action.overrideFailed' },
  { value: StaffServiceAgent.AuditAction.OverridePinChanged, name: 'auditLog.action.overridePinChanged' },
  { value: StaffServiceAgent.AuditAction.PriceOverride, name: 'auditLog.action.priceOverride' },
  { value: StaffServiceAgent.AuditAction.Refund, name: 'auditLog.action.refund' },
  { value: StaffServiceAgent.AuditAction.Exchange, name: 'auditLog.action.exchange' },
  { value: StaffServiceAgent.AuditAction.Reprint, name: 'auditLog.action.reprint' },
  { value: StaffServiceAgent.AuditAction.VoidSale, name: 'auditLog.action.voidSale' },
  { value: StaffServiceAgent.AuditAction.CashPayOut, name: 'auditLog.action.cashPayOut' },
  { value: StaffServiceAgent.AuditAction.DrawerReconcile, name: 'auditLog.action.drawerReconcile' },
  { value: StaffServiceAgent.AuditAction.Compensation, name: 'auditLog.action.compensation' },
  { value: StaffServiceAgent.AuditAction.PointsAdjust, name: 'auditLog.action.pointsAdjust' },
  { value: StaffServiceAgent.AuditAction.ResendTicket, name: 'auditLog.action.resendTicket' },
  { value: StaffServiceAgent.AuditAction.BlockSeat, name: 'auditLog.action.blockSeat' },
  { value: StaffServiceAgent.AuditAction.BlockRoom, name: 'auditLog.action.blockRoom' },
  { value: StaffServiceAgent.AuditAction.TicketAdmitOverride, name: 'auditLog.action.ticketAdmitOverride' },
];

/** i18n label key for an AuditAction value. */
export function auditActionLabel(action?: StaffServiceAgent.AuditAction): string {
  return AuditActionValues.find(v => v.value === action)?.name ?? AuditActionValues[0].name;
}

/** CSS pill class for an AuditAction: failures red, money-affecting actions amber, the rest neutral. */
export function auditActionPillClass(action?: StaffServiceAgent.AuditAction): string {
  switch (action) {
    case StaffServiceAgent.AuditAction.OverrideFailed: return 'ad-pill--danger';
    case StaffServiceAgent.AuditAction.PriceOverride:
    case StaffServiceAgent.AuditAction.Refund:
    case StaffServiceAgent.AuditAction.VoidSale:
    case StaffServiceAgent.AuditAction.CashPayOut:
    case StaffServiceAgent.AuditAction.Compensation:
    case StaffServiceAgent.AuditAction.PointsAdjust:
    case StaffServiceAgent.AuditAction.TicketAdmitOverride: return 'ad-pill--warn';
    default: return 'ad-pill--neutral';
  }
}

// ── Operations and workforce (staff app) ─────────────────────────────────────────────────────────────

/** i18n-key label for each Operations IncidentCategory value. */
export const IncidentCategoryValues: { value: StaffServiceAgent.IncidentCategory; name: string }[] = [
  { value: StaffServiceAgent.IncidentCategory.Other, name: 'staffEnums.incidentCategory.other' },
  { value: StaffServiceAgent.IncidentCategory.Seat, name: 'staffEnums.incidentCategory.seat' },
  { value: StaffServiceAgent.IncidentCategory.Room, name: 'staffEnums.incidentCategory.room' },
  { value: StaffServiceAgent.IncidentCategory.Projection, name: 'staffEnums.incidentCategory.projection' },
  { value: StaffServiceAgent.IncidentCategory.Sound, name: 'staffEnums.incidentCategory.sound' },
  { value: StaffServiceAgent.IncidentCategory.Safety, name: 'staffEnums.incidentCategory.safety' },
  { value: StaffServiceAgent.IncidentCategory.Customer, name: 'staffEnums.incidentCategory.customer' },
  { value: StaffServiceAgent.IncidentCategory.Cleanliness, name: 'staffEnums.incidentCategory.cleanliness' },
];

/** i18n key for an incident category (falls back to Other). */
export function incidentCategoryLabel(c?: StaffServiceAgent.IncidentCategory): string {
  return IncidentCategoryValues.find(v => v.value === c)?.name ?? IncidentCategoryValues[0].name;
}

/** i18n-key label for each IncidentSeverity value. */
export const IncidentSeverityValues: { value: StaffServiceAgent.IncidentSeverity; name: string }[] = [
  { value: StaffServiceAgent.IncidentSeverity.Low, name: 'staffEnums.incidentSeverity.low' },
  { value: StaffServiceAgent.IncidentSeverity.Medium, name: 'staffEnums.incidentSeverity.medium' },
  { value: StaffServiceAgent.IncidentSeverity.High, name: 'staffEnums.incidentSeverity.high' },
  { value: StaffServiceAgent.IncidentSeverity.Critical, name: 'staffEnums.incidentSeverity.critical' },
];

/** i18n key for an incident severity (falls back to Low). */
export function incidentSeverityLabel(s?: StaffServiceAgent.IncidentSeverity): string {
  return IncidentSeverityValues.find(v => v.value === s)?.name ?? IncidentSeverityValues[0].name;
}

/** CSS pill class for each IncidentSeverity value. */
export function incidentSeverityPillClass(s?: StaffServiceAgent.IncidentSeverity): string {
  switch (s) {
    case StaffServiceAgent.IncidentSeverity.Critical: return 'ad-pill--danger';
    case StaffServiceAgent.IncidentSeverity.High: return 'ad-pill--warn';
    case StaffServiceAgent.IncidentSeverity.Medium: return 'ad-pill--violet';
    default: return 'ad-pill--neutral';
  }
}

/** i18n-key label for each IncidentStatus value. */
export const IncidentStatusValues: { value: StaffServiceAgent.IncidentStatus; name: string }[] = [
  { value: StaffServiceAgent.IncidentStatus.Open, name: 'staffEnums.incidentStatus.open' },
  { value: StaffServiceAgent.IncidentStatus.Resolved, name: 'staffEnums.incidentStatus.resolved' },
];

/** i18n key for an incident status (falls back to Open). */
export function incidentStatusLabel(s?: StaffServiceAgent.IncidentStatus): string {
  return IncidentStatusValues.find(v => v.value === s)?.name ?? IncidentStatusValues[0].name;
}

/** CSS pill class for each IncidentStatus value. */
export function incidentStatusPillClass(s?: StaffServiceAgent.IncidentStatus): string {
  return s === StaffServiceAgent.IncidentStatus.Resolved ? 'ad-pill--success' : 'ad-pill--warn';
}

/** i18n-key label for each ChecklistKind value. */
export const ChecklistKindValues: { value: StaffServiceAgent.ChecklistKind; name: string }[] = [
  { value: StaffServiceAgent.ChecklistKind.PreShow, name: 'staffEnums.checklistKind.preShow' },
  { value: StaffServiceAgent.ChecklistKind.PostShow, name: 'staffEnums.checklistKind.postShow' },
];

/** i18n key for a checklist kind (falls back to PreShow). */
export function checklistKindLabel(k?: StaffServiceAgent.ChecklistKind): string {
  return ChecklistKindValues.find(v => v.value === k)?.name ?? ChecklistKindValues[0].name;
}

/** i18n-key label for each StaffTaskStatus value. */
export const StaffTaskStatusValues: { value: StaffServiceAgent.StaffTaskStatus; name: string }[] = [
  { value: StaffServiceAgent.StaffTaskStatus.Open, name: 'staffEnums.taskStatus.open' },
  { value: StaffServiceAgent.StaffTaskStatus.InProgress, name: 'staffEnums.taskStatus.inProgress' },
  { value: StaffServiceAgent.StaffTaskStatus.Done, name: 'staffEnums.taskStatus.done' },
  { value: StaffServiceAgent.StaffTaskStatus.Cancelled, name: 'staffEnums.taskStatus.cancelled' },
];

/** i18n key for a staff task status (falls back to Open). */
export function staffTaskStatusLabel(s?: StaffServiceAgent.StaffTaskStatus): string {
  return StaffTaskStatusValues.find(v => v.value === s)?.name ?? StaffTaskStatusValues[0].name;
}

/** CSS pill class for each StaffTaskStatus value. */
export function staffTaskStatusPillClass(s?: StaffServiceAgent.StaffTaskStatus): string {
  switch (s) {
    case StaffServiceAgent.StaffTaskStatus.Done: return 'ad-pill--success';
    case StaffServiceAgent.StaffTaskStatus.InProgress: return 'ad-pill--violet';
    case StaffServiceAgent.StaffTaskStatus.Open: return 'ad-pill--warn';
    default: return 'ad-pill--neutral';
  }
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
    case 'storagePlan': {
      const status = value as CinemaServiceAgent.StoragePlanStatus;
      return { labelKey: storagePlanStatusLabel(status), cssClass: storagePlanStatusPillClass(status) };
    }
    case 'scanOutcome': {
      const spec = scanOutcomeSpec(value as StaffServiceAgent.ScanOutcome);
      return { labelKey: spec.labelKey, cssClass: scanToneCssClass(spec.tone) };
    }
    case 'auditAction': {
      const action = value as StaffServiceAgent.AuditAction;
      return { labelKey: auditActionLabel(action), cssClass: auditActionPillClass(action) };
    }
    case 'foodOrder': {
      return foodOrderStatusSpec(value as StaffServiceAgent.FoodOrderStatus);
    }
    case 'drawerStatus': {
      const status = value as StaffServiceAgent.CashDrawerStatus;
      return {
        labelKey: CashDrawerStatusValues.find(v => v.value === status)?.name ?? CashDrawerStatusValues[0].name,
        cssClass: cashDrawerStatusPillClass(status),
      };
    }
    case 'stockLevel': {
      return StockLevelPills[(value as StockLevel) ?? 'untracked'] ?? StockLevelPills.untracked;
    }
    case 'incident': {
      const status = value as StaffServiceAgent.IncidentStatus;
      return { labelKey: incidentStatusLabel(status), cssClass: incidentStatusPillClass(status) };
    }
    case 'incidentSeverity': {
      const severity = value as StaffServiceAgent.IncidentSeverity;
      return { labelKey: incidentSeverityLabel(severity), cssClass: incidentSeverityPillClass(severity) };
    }
    case 'staffTask': {
      const status = value as StaffServiceAgent.StaffTaskStatus;
      return { labelKey: staffTaskStatusLabel(status), cssClass: staffTaskStatusPillClass(status) };
    }
    case 'roomStatus': {
      const status = value as number;
      return { labelKey: roomStatusLabel(status), cssClass: roomStatusPillClass(status) };
    }
  }
}


/** i18n-key label for each counter payment tender a cashier can take. */
export const CounterTenderValues: { value: StaffServiceAgent.PaymentTender; name: string }[] = [
  { value: StaffServiceAgent.PaymentTender.Cash, name: 'pos.tender.cash' },
  { value: StaffServiceAgent.PaymentTender.Card, name: 'pos.tender.card' },
  { value: StaffServiceAgent.PaymentTender.QrWallet, name: 'pos.tender.qrWallet' },
];

/** i18n-key label for each cash-drawer movement type. */
export const CashMovementTypeValues: { value: StaffServiceAgent.CashMovementType; name: string }[] = [
  { value: StaffServiceAgent.CashMovementType.OpeningFloat, name: 'drawer.movement.openingFloat' },
  { value: StaffServiceAgent.CashMovementType.Sale, name: 'drawer.movement.sale' },
  { value: StaffServiceAgent.CashMovementType.Refund, name: 'drawer.movement.refund' },
  { value: StaffServiceAgent.CashMovementType.PayIn, name: 'drawer.movement.payIn' },
  { value: StaffServiceAgent.CashMovementType.PayOut, name: 'drawer.movement.payOut' },
];

export function cashMovementTypeLabel(type?: StaffServiceAgent.CashMovementType): string {
  return CashMovementTypeValues.find(v => v.value === type)?.name ?? CashMovementTypeValues[0].name;
}

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
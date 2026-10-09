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
export const StaffReasonCodeValues: { value: CinemaServiceAgent.StaffReasonCode; name: string }[] = [
  { value: CinemaServiceAgent.StaffReasonCode.CustomerRequest, name: 'staffReason.customerRequest' },
  { value: CinemaServiceAgent.StaffReasonCode.WrongShowtime, name: 'staffReason.wrongShowtime' },
  { value: CinemaServiceAgent.StaffReasonCode.DuplicateSale, name: 'staffReason.duplicateSale' },
  { value: CinemaServiceAgent.StaffReasonCode.ServiceFailure, name: 'staffReason.serviceFailure' },
  { value: CinemaServiceAgent.StaffReasonCode.TechnicalIssue, name: 'staffReason.technicalIssue' },
  { value: CinemaServiceAgent.StaffReasonCode.PriceMatch, name: 'staffReason.priceMatch' },
  { value: CinemaServiceAgent.StaffReasonCode.Compensation, name: 'staffReason.compensation' },
  { value: CinemaServiceAgent.StaffReasonCode.Other, name: 'staffReason.other' },
];

/** i18n-key label for each PaymentTender. */
export const PaymentTenderValues: { value: CinemaServiceAgent.PaymentTender; name: string }[] = [
  { value: CinemaServiceAgent.PaymentTender.Cash, name: 'tender.cash' },
  { value: CinemaServiceAgent.PaymentTender.Card, name: 'tender.card' },
  { value: CinemaServiceAgent.PaymentTender.QrWallet, name: 'tender.qrWallet' },
  { value: CinemaServiceAgent.PaymentTender.GiftCard, name: 'tender.giftCard' },
  { value: CinemaServiceAgent.PaymentTender.Points, name: 'tender.points' },
  { value: CinemaServiceAgent.PaymentTender.Online, name: 'tender.online' },
];

/** i18n key of a PaymentTender (falls back to the first entry for an unknown value). */
export function paymentTenderLabel(tender?: CinemaServiceAgent.PaymentTender): string {
  return PaymentTenderValues.find(v => v.value === tender)?.name ?? PaymentTenderValues[0].name;
}

/** Tenders a refund can be paid back through at the counter. */
export const RefundTenderValues: { value: CinemaServiceAgent.PaymentTender; name: string }[] = PaymentTenderValues
  .filter(v => v.value === CinemaServiceAgent.PaymentTender.Cash
    || v.value === CinemaServiceAgent.PaymentTender.Card
    || v.value === CinemaServiceAgent.PaymentTender.QrWallet);

/** i18n-key label for each CashDrawerStatus. */
export const CashDrawerStatusValues: { value: CinemaServiceAgent.CashDrawerStatus; name: string }[] = [
  { value: CinemaServiceAgent.CashDrawerStatus.Open, name: 'drawerStatus.open' },
  { value: CinemaServiceAgent.CashDrawerStatus.Closed, name: 'drawerStatus.closed' },
  { value: CinemaServiceAgent.CashDrawerStatus.Reconciled, name: 'drawerStatus.reconciled' },
];

/** CSS pill class for a CashDrawerStatus: open amber, closed (awaiting reconciliation) red, reconciled green. */
export function cashDrawerStatusPillClass(s?: CinemaServiceAgent.CashDrawerStatus): string {
  switch (s) {
    case CinemaServiceAgent.CashDrawerStatus.Reconciled: return 'ad-pill--success';
    case CinemaServiceAgent.CashDrawerStatus.Open: return 'ad-pill--warn';
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
export type StatusPillKind = 'invoice' | 'storagePlan' | 'stockLevel' | 'scanOutcome' | 'auditAction' | 'incident' | 'incidentSeverity' | 'staffTask' | 'roomStatus' | 'foodOrder' | 'drawerStatus' | 'complaint';

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
  { value: CinemaServiceAgent.AuditAction.Refund, name: 'auditLog.action.refund' },
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
    case CinemaServiceAgent.AuditAction.Refund:
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

/** i18n-key label for each ChecklistKind value. */
export const ChecklistKindValues: { value: CinemaServiceAgent.ChecklistKind; name: string }[] = [
  { value: CinemaServiceAgent.ChecklistKind.PreShow, name: 'staffEnums.checklistKind.preShow' },
  { value: CinemaServiceAgent.ChecklistKind.PostShow, name: 'staffEnums.checklistKind.postShow' },
];

/** i18n key for a checklist kind (falls back to PreShow). */
export function checklistKindLabel(k?: CinemaServiceAgent.ChecklistKind): string {
  return ChecklistKindValues.find(v => v.value === k)?.name ?? ChecklistKindValues[0].name;
}

/** i18n-key label for each StaffTaskStatus value. */
export const StaffTaskStatusValues: { value: CinemaServiceAgent.StaffTaskStatus; name: string }[] = [
  { value: CinemaServiceAgent.StaffTaskStatus.Open, name: 'staffEnums.taskStatus.open' },
  { value: CinemaServiceAgent.StaffTaskStatus.InProgress, name: 'staffEnums.taskStatus.inProgress' },
  { value: CinemaServiceAgent.StaffTaskStatus.Done, name: 'staffEnums.taskStatus.done' },
  { value: CinemaServiceAgent.StaffTaskStatus.Cancelled, name: 'staffEnums.taskStatus.cancelled' },
];

/** i18n key for a staff task status (falls back to Open). */
export function staffTaskStatusLabel(s?: CinemaServiceAgent.StaffTaskStatus): string {
  return StaffTaskStatusValues.find(v => v.value === s)?.name ?? StaffTaskStatusValues[0].name;
}

/** CSS pill class for each StaffTaskStatus value. */
export function staffTaskStatusPillClass(s?: CinemaServiceAgent.StaffTaskStatus): string {
  switch (s) {
    case CinemaServiceAgent.StaffTaskStatus.Done: return 'ad-pill--success';
    case CinemaServiceAgent.StaffTaskStatus.InProgress: return 'ad-pill--violet';
    case CinemaServiceAgent.StaffTaskStatus.Open: return 'ad-pill--warn';
    default: return 'ad-pill--neutral';
  }
}

/** i18n-key label for each ComplaintStatus value. */
export const ComplaintStatusValues: { value: CinemaServiceAgent.ComplaintStatus; name: string }[] = [
  { value: CinemaServiceAgent.ComplaintStatus.Open, name: 'staffEnums.complaintStatus.open' },
  { value: CinemaServiceAgent.ComplaintStatus.InReview, name: 'staffEnums.complaintStatus.inReview' },
  { value: CinemaServiceAgent.ComplaintStatus.Resolved, name: 'staffEnums.complaintStatus.resolved' },
  { value: CinemaServiceAgent.ComplaintStatus.Rejected, name: 'staffEnums.complaintStatus.rejected' },
];

/** i18n key for a complaint status (falls back to Open). */
export function complaintStatusLabel(s?: CinemaServiceAgent.ComplaintStatus): string {
  return ComplaintStatusValues.find(v => v.value === s)?.name ?? ComplaintStatusValues[0].name;
}

/** CSS pill class for each ComplaintStatus value. */
export function complaintStatusPillClass(s?: CinemaServiceAgent.ComplaintStatus): string {
  switch (s) {
    case CinemaServiceAgent.ComplaintStatus.Resolved: return 'ad-pill--success';
    case CinemaServiceAgent.ComplaintStatus.InReview: return 'ad-pill--violet';
    case CinemaServiceAgent.ComplaintStatus.Rejected: return 'ad-pill--neutral';
    default: return 'ad-pill--warn';
  }
}

/** i18n-key label for each ComplaintCategory value. */
export const ComplaintCategoryValues: { value: CinemaServiceAgent.ComplaintCategory; name: string }[] = [
  { value: CinemaServiceAgent.ComplaintCategory.Other, name: 'staffEnums.complaintCategory.other' },
  { value: CinemaServiceAgent.ComplaintCategory.Service, name: 'staffEnums.complaintCategory.service' },
  { value: CinemaServiceAgent.ComplaintCategory.Booking, name: 'staffEnums.complaintCategory.booking' },
  { value: CinemaServiceAgent.ComplaintCategory.Payment, name: 'staffEnums.complaintCategory.payment' },
  { value: CinemaServiceAgent.ComplaintCategory.Projection, name: 'staffEnums.complaintCategory.projection' },
  { value: CinemaServiceAgent.ComplaintCategory.Sound, name: 'staffEnums.complaintCategory.sound' },
  { value: CinemaServiceAgent.ComplaintCategory.FoodAndDrink, name: 'staffEnums.complaintCategory.foodAndDrink' },
  { value: CinemaServiceAgent.ComplaintCategory.Facilities, name: 'staffEnums.complaintCategory.facilities' },
  { value: CinemaServiceAgent.ComplaintCategory.Staff, name: 'staffEnums.complaintCategory.staff' },
];

/** i18n key for a complaint category (falls back to Other). */
export function complaintCategoryLabel(c?: CinemaServiceAgent.ComplaintCategory): string {
  return ComplaintCategoryValues.find(v => v.value === c)?.name ?? ComplaintCategoryValues[0].name;
}

/** i18n-key label for each ComplaintResolution value. */
export const ComplaintResolutionValues: { value: CinemaServiceAgent.ComplaintResolution; name: string }[] = [
  { value: CinemaServiceAgent.ComplaintResolution.None, name: 'staffEnums.complaintResolution.none' },
  { value: CinemaServiceAgent.ComplaintResolution.Refund, name: 'staffEnums.complaintResolution.refund' },
  { value: CinemaServiceAgent.ComplaintResolution.GiftCard, name: 'staffEnums.complaintResolution.giftCard' },
  { value: CinemaServiceAgent.ComplaintResolution.Points, name: 'staffEnums.complaintResolution.points' },
  { value: CinemaServiceAgent.ComplaintResolution.Apology, name: 'staffEnums.complaintResolution.apology' },
];

/** i18n key for a complaint resolution (falls back to None). */
export function complaintResolutionLabel(r?: CinemaServiceAgent.ComplaintResolution): string {
  return ComplaintResolutionValues.find(v => v.value === r)?.name ?? ComplaintResolutionValues[0].name;
}

/** i18n-key label for each ETicketChannel value. */
export const ETicketChannelValues: { value: CinemaServiceAgent.ETicketChannel; name: string }[] = [
  { value: CinemaServiceAgent.ETicketChannel.Email, name: 'staffEnums.eTicketChannel.email' },
  { value: CinemaServiceAgent.ETicketChannel.Sms, name: 'staffEnums.eTicketChannel.sms' },
];

/** i18n key for an e-ticket channel (falls back to Email). */
export function eTicketChannelLabel(c?: CinemaServiceAgent.ETicketChannel): string {
  return ETicketChannelValues.find(v => v.value === c)?.name ?? ETicketChannelValues[0].name;
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
    case 'drawerStatus': {
      const status = value as CinemaServiceAgent.CashDrawerStatus;
      return {
        labelKey: CashDrawerStatusValues.find(v => v.value === status)?.name ?? CashDrawerStatusValues[0].name,
        cssClass: cashDrawerStatusPillClass(status),
      };
    }
    case 'stockLevel': {
      return StockLevelPills[(value as StockLevel) ?? 'untracked'] ?? StockLevelPills.untracked;
    }
    case 'incident': {
      const status = value as CinemaServiceAgent.IncidentStatus;
      return { labelKey: incidentStatusLabel(status), cssClass: incidentStatusPillClass(status) };
    }
    case 'incidentSeverity': {
      const severity = value as CinemaServiceAgent.IncidentSeverity;
      return { labelKey: incidentSeverityLabel(severity), cssClass: incidentSeverityPillClass(severity) };
    }
    case 'staffTask': {
      const status = value as CinemaServiceAgent.StaffTaskStatus;
      return { labelKey: staffTaskStatusLabel(status), cssClass: staffTaskStatusPillClass(status) };
    }
    case 'roomStatus': {
      const status = value as number;
      return { labelKey: roomStatusLabel(status), cssClass: roomStatusPillClass(status) };
    }
    case 'complaint': {
      const status = value as CinemaServiceAgent.ComplaintStatus;
      return { labelKey: complaintStatusLabel(status), cssClass: complaintStatusPillClass(status) };
    }
  }
}


/** i18n-key label for each counter payment tender a cashier can take. */
export const CounterTenderValues: { value: CinemaServiceAgent.PaymentTender; name: string }[] = [
  { value: CinemaServiceAgent.PaymentTender.Cash, name: 'pos.tender.cash' },
  { value: CinemaServiceAgent.PaymentTender.Card, name: 'pos.tender.card' },
  { value: CinemaServiceAgent.PaymentTender.QrWallet, name: 'pos.tender.qrWallet' },
];

/** i18n-key label for each cash-drawer movement type. */
export const CashMovementTypeValues: { value: CinemaServiceAgent.CashMovementType; name: string }[] = [
  { value: CinemaServiceAgent.CashMovementType.OpeningFloat, name: 'drawer.movement.openingFloat' },
  { value: CinemaServiceAgent.CashMovementType.Sale, name: 'drawer.movement.sale' },
  { value: CinemaServiceAgent.CashMovementType.Refund, name: 'drawer.movement.refund' },
  { value: CinemaServiceAgent.CashMovementType.PayIn, name: 'drawer.movement.payIn' },
  { value: CinemaServiceAgent.CashMovementType.PayOut, name: 'drawer.movement.payOut' },
];

export function cashMovementTypeLabel(type?: CinemaServiceAgent.CashMovementType): string {
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
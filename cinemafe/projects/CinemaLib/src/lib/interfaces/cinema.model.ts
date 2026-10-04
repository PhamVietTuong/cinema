import { CinemaServiceAgent } from '../services/cinema-http.service';
import { StaffServiceAgent } from '../services/staff-http.service';

/** Display role shown in the admin user list (derived from UserDTO.userTypeName). */
export enum UserRole {
  Admin = 'Admin',
  Customer = 'Khách Hàng',
  TheaterStaff = 'Nhân viên rạp',
  TheaterManager = 'Quản lý rạp',
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
export type StatusPillKind = 'invoice' | 'storagePlan' | 'stockLevel' | 'incident' | 'incidentSeverity' | 'staffTask' | 'roomStatus';

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
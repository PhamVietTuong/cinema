import { CinemaServiceAgent } from '../services/cinema-http.service';

/** Display role shown in the admin user list (derived from UserDTO.userTypeName). */
export enum UserRole {
  Admin = 'Admin',
  Customer = 'Khách Hàng',
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

/** CSS pill class for each Invoice.InvoiceStatus value (used by the admin invoices grid). */
export function invoiceStatusPillClass(s?: CinemaServiceAgent.InvoiceStatus): string {
  switch (s) {
    case CinemaServiceAgent.InvoiceStatus.Paid: return 'ad-pill--success';
    case CinemaServiceAgent.InvoiceStatus.Pending: return 'ad-pill--warn';
    case CinemaServiceAgent.InvoiceStatus.Refunded: return 'ad-pill--neutral';
    default: return 'ad-pill--danger';
  }
}
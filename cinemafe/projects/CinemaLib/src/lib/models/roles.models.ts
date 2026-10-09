/** Role names, equal to UserType.Name in the JWT / UserDTO.userTypeName. Mirrors cinemabe RoleNames. */
export const UserRoles = {
  Admin: 'Admin',
  Customer: 'Customer',
  TheaterStaff: 'TheaterStaff',
  TheaterManager: 'TheaterManager',
  BoxOfficeStaff: 'BoxOfficeStaff',
  GateStaff: 'GateStaff',
  KitchenStaff: 'KitchenStaff',
  RegionalManager: 'RegionalManager',
} as const;

/**
 * Capability composites, kept in sync with `RoleNames` in cinemabe/Cinema/2-Business/Cinema.Business.DTO/Auth/RoleNames.cs.
 * One role per user; what each role may do is defined here and on the API ([Authorize(Roles = ...)]), not in the database.
 */

/** Warehouse (inventory + storage plans). Gate and box-office staff are not included. */
export const BACK_OFFICE_ROLES: readonly string[] = [
  UserRoles.Admin,
  UserRoles.TheaterManager,
  UserRoles.TheaterStaff,
  UserRoles.KitchenStaff,
];

/** Roles allowed to approve / reject storage (restock) plans and change inventory tracking settings. */
export const STOCK_APPROVER_ROLES: readonly string[] = [
  UserRoles.Admin,
  UserRoles.TheaterManager,
];

/** Anyone allowed to sign in to the CinemaStaff app. */
export const STAFF_APP_ROLES: readonly string[] = [
  UserRoles.Admin,
  UserRoles.RegionalManager,
  UserRoles.TheaterManager,
  UserRoles.TheaterStaff,
  UserRoles.BoxOfficeStaff,
  UserRoles.GateStaff,
  UserRoles.KitchenStaff,
];

/** Counter point of sale, cash drawer, reprint and customer lookup. */
export const SELLER_ROLES: readonly string[] = [
  UserRoles.Admin,
  UserRoles.TheaterManager,
  UserRoles.TheaterStaff,
  UserRoles.BoxOfficeStaff,
];

/** Customer-service complaints (create, review, resolve): sellers plus regional managers. Mirrors the CustomerServiceController. */
export const COMPLAINT_ROLES: readonly string[] = [
  ...SELLER_ROLES,
  UserRoles.RegionalManager,
];

/** Ticket scanning / admission at the gate. */
export const GATE_KEEPER_ROLES: readonly string[] = [
  UserRoles.Admin,
  UserRoles.TheaterManager,
  UserRoles.TheaterStaff,
  UserRoles.GateStaff,
  UserRoles.BoxOfficeStaff,
];

/** Food and drink counter sale and the pickup queue. */
export const CONCESSION_ROLES: readonly string[] = [
  UserRoles.Admin,
  UserRoles.TheaterManager,
  UserRoles.TheaterStaff,
  UserRoles.KitchenStaff,
  UserRoles.BoxOfficeStaff,
];

/** Manager override PIN, drawer reconciliation, compensation, rosters, checklist templates. */
export const APPROVER_ROLES: readonly string[] = [
  UserRoles.Admin,
  UserRoles.RegionalManager,
  UserRoles.TheaterManager,
];

/** Management reporting (daily close, sales, audit log). */
export const REPORTING_ROLES: readonly string[] = [
  UserRoles.Admin,
  UserRoles.RegionalManager,
  UserRoles.TheaterManager,
];

/** Roles whose account must belong to a theater (every staff role except Admin and RegionalManager). */
export const THEATER_SCOPED_ROLES: readonly string[] = [
  UserRoles.TheaterManager,
  UserRoles.TheaterStaff,
  UserRoles.BoxOfficeStaff,
  UserRoles.GateStaff,
  UserRoles.KitchenStaff,
];

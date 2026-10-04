/** Role names, equal to UserType.Name in the JWT / UserDTO.userTypeName. */
export const UserRoles = {
  Admin: 'Admin',
  Customer: 'Customer',
  TheaterStaff: 'TheaterStaff',
  TheaterManager: 'TheaterManager',
} as const;

/** Roles allowed into the admin back office. */
export const BACK_OFFICE_ROLES: readonly string[] = [
  UserRoles.Admin,
  UserRoles.TheaterManager,
  UserRoles.TheaterStaff,
];

/** Roles allowed to approve / reject storage (restock) plans. */
export const STOCK_APPROVER_ROLES: readonly string[] = [
  UserRoles.Admin,
  UserRoles.TheaterManager,
];

/** Roles allowed to sign in to the CinemaStaff app (existing roles only until the staff roles land). */
export const STAFF_APP_ROLES: readonly string[] = [
  UserRoles.Admin,
  UserRoles.TheaterManager,
  UserRoles.TheaterStaff,
];

namespace Cinema.Business.DTO.Auth;

/// <summary>
/// The <c>UserType.Name</c> values, which are also the JWT role claim. Composite strings are for
/// <c>[Authorize(Roles = ...)]</c> (comma = any of). Pure constants: capability checks stay in code, there is no
/// permission table. Mirrored on the frontend in <c>roles.models.ts</c>.
/// </summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";
    public const string TheaterStaff = "TheaterStaff";

    /// <summary>Anyone allowed to log in to the CinemaStaff app.</summary>
    public const string StaffApp = Admin + "," + TheaterStaff;

    /// <summary>POS, cash drawer, reprint and customer lookup.</summary>
    public const string Sellers = Admin + "," + TheaterStaff;

    /// <summary>Ticket scanning / admission at the gate.</summary>
    public const string GateKeepers = Admin + "," + TheaterStaff;

    /// <summary>Food and drink counter sale and the pickup queue.</summary>
    public const string Concession = Admin + "," + TheaterStaff;

    /// <summary>Anyone allowed into the warehouse/inventory part of the staff app.</summary>
    public const string BackOffice = Admin + "," + TheaterStaff;

    /// <summary>Roles that may approve/reject storage plans and change inventory tracking settings.</summary>
    public const string StockApprovers = Admin;

    /// <summary>Override PIN approvers, drawer reconciliation, compensation, rosters.</summary>
    public const string Approvers = Admin;

    /// <summary>Management reporting (daily close, sales, audit log).</summary>
    public const string Reporting = Admin;

    /// <summary>Roles whose account must belong to a theater (every staff role except Admin).</summary>
    public const string TheaterScopedRoles = TheaterStaff;
}

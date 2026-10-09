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
    public const string TheaterManager = "TheaterManager";
    public const string BoxOfficeStaff = "BoxOfficeStaff";
    public const string GateStaff = "GateStaff";
    public const string KitchenStaff = "KitchenStaff";
    public const string RegionalManager = "RegionalManager";

    /// <summary>Anyone allowed to log in to the CinemaStaff app.</summary>
    public const string StaffApp = Admin + "," + RegionalManager + "," + TheaterManager + "," + TheaterStaff
        + "," + BoxOfficeStaff + "," + GateStaff + "," + KitchenStaff;

    /// <summary>POS, cash drawer, reprint and customer lookup.</summary>
    public const string Sellers = Admin + "," + TheaterManager + "," + TheaterStaff + "," + BoxOfficeStaff;

    /// <summary>Ticket scanning / admission at the gate.</summary>
    public const string GateKeepers = Admin + "," + TheaterManager + "," + TheaterStaff + "," + GateStaff + "," + BoxOfficeStaff;

    /// <summary>Food and drink counter sale and the pickup queue.</summary>
    public const string Concession = Admin + "," + TheaterManager + "," + TheaterStaff + "," + KitchenStaff + "," + BoxOfficeStaff;

    /// <summary>Anyone allowed into the warehouse/inventory part of the staff app.</summary>
    public const string BackOffice = Admin + "," + TheaterManager + "," + TheaterStaff + "," + KitchenStaff;

    /// <summary>Roles that may approve/reject storage plans and change inventory tracking settings.</summary>
    public const string StockApprovers = Admin + "," + TheaterManager;

    /// <summary>Override PIN approvers, drawer reconciliation, compensation, rosters.</summary>
    public const string Approvers = Admin + "," + RegionalManager + "," + TheaterManager;

    /// <summary>Management reporting (daily close, sales, audit log).</summary>
    public const string Reporting = Admin + "," + RegionalManager + "," + TheaterManager;

    /// <summary>Roles whose account must belong to a theater (every staff role except Admin and RegionalManager).</summary>
    public const string TheaterScopedRoles = TheaterManager + "," + TheaterStaff + "," + BoxOfficeStaff + "," + GateStaff + "," + KitchenStaff;
}

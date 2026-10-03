namespace Cinema.Business.DTO.Auth;

/// <summary>
/// The <c>UserType.Name</c> values, which are also the JWT role claim. Composite strings are for
/// <c>[Authorize(Roles = ...)]</c> (comma = any of).
/// </summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";
    public const string TheaterStaff = "TheaterStaff";
    public const string TheaterManager = "TheaterManager";

    /// <summary>Anyone allowed into the warehouse/inventory part of the admin app.</summary>
    public const string BackOffice = Admin + "," + TheaterManager + "," + TheaterStaff;

    /// <summary>Roles that may approve/reject storage plans and change inventory tracking settings.</summary>
    public const string StockApprovers = Admin + "," + TheaterManager;
}

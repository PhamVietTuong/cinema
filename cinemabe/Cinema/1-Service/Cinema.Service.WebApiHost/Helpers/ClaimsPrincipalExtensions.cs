using System.Security.Claims;

namespace Cinema.Service.WebApiHost.Helpers;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? principal.FindFirstValue("sub")
                    ?? throw new UnauthorizedAccessException("User ID claim not found.");
        return Guid.Parse(value);
    }

    public static string GetUserRole(this ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    public static bool IsAdmin(this ClaimsPrincipal principal)
        => principal.IsInRole("Admin");

    public static bool IsTheaterStaff(this ClaimsPrincipal principal)
        => principal.IsInRole("TheaterStaff");

    /// <summary>The theater a staff account manages, or null (admins/customers have no scope).</summary>
    public static Guid? GetTheaterId(this ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue("theaterId"), out var id) ? id : null;
}

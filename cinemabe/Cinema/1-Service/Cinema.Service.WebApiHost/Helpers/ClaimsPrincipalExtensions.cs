using System.Security.Claims;
using Cinema.Business.DTO.Auth;

namespace Cinema.Service.WebApiHost.Helpers;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? principal.FindFirstValue("sub");
        if (value == null)
        {
            throw new UnauthorizedAccessException("User ID claim not found.");
        }
        return Guid.Parse(value);
    }

    public static string GetUserRole(this ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    public static bool IsAdmin(this ClaimsPrincipal principal)
        => principal.IsInRole(RoleNames.Admin);

    public static bool IsTheaterStaff(this ClaimsPrincipal principal)
        => principal.IsInRole(RoleNames.TheaterStaff);

    public static bool IsTheaterManager(this ClaimsPrincipal principal)
        => principal.IsInRole(RoleNames.TheaterManager);

    /// <summary>The theater a staff account manages, or null (admins/customers have no scope).</summary>
    public static Guid? GetTheaterId(this ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue("theaterId"), out var id) ? id : null;

    /// <summary>
    /// Resolves the data scope of a back-office caller. Admins see every theater (<paramref name="theaterScope"/>
    /// = null). Theater staff/managers are pinned to their own theater. Returns false — caller should answer
    /// 403, never 401 (the frontend logs the user out on 401) — when a staff/manager token has no theater claim
    /// or the caller is not a back-office role at all.
    /// </summary>
    public static bool TryGetBackOfficeScope(this ClaimsPrincipal principal, out Guid? theaterScope)
    {
        theaterScope = null;
        if (principal.IsAdmin())
        {
            return true;
        }
        if (principal.IsTheaterStaff() || principal.IsTheaterManager())
        {
            theaterScope = principal.GetTheaterId();
            return theaterScope != null;
        }
        return false;
    }
}

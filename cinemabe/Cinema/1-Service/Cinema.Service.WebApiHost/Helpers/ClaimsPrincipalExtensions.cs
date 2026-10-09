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

    /// <summary>The theater a staff account manages, or null (admins/customers have no scope).</summary>
    public static Guid? GetTheaterId(this ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue("theaterId"), out var id) ? id : null;

    /// <summary>Every theater a staff account may act on: one <c>theaterId</c> claim per theater.</summary>
    public static IReadOnlyCollection<Guid> GetTheaterIds(this ClaimsPrincipal principal)
        => principal.FindAll("theaterId")
            .Select(c => Guid.TryParse(c.Value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

    private static readonly string[] _theaterScopedRoles = RoleNames.TheaterScopedRoles.Split(',');
    private static readonly string[] _backOfficeRoles = RoleNames.BackOffice.Split(',');

    private static bool IsInAnyRole(ClaimsPrincipal principal, IEnumerable<string> roles)
        => roles.Any(principal.IsInRole);

    /// <summary>
    /// Resolves the data scope of a back-office (warehouse) caller. Admins see every theater
    /// (<paramref name="theaterScope"/> = null). Back-office staff (<see cref="RoleNames.BackOffice"/>: theater
    /// staff, managers and kitchen staff) are pinned to their own theater. Returns false — caller should answer
    /// 403, never 401 (the frontend logs the user out on 401) — when such a token has no theater claim or the
    /// caller is not a back-office role at all (e.g. gate staff).
    /// </summary>
    public static bool TryGetBackOfficeScope(this ClaimsPrincipal principal, out Guid? theaterScope)
    {
        theaterScope = null;
        if (principal.IsAdmin())
        {
            return true;
        }
        if (IsInAnyRole(principal, _backOfficeRoles))
        {
            theaterScope = principal.GetTheaterId();
            return theaterScope != null;
        }
        return false;
    }

    /// <summary>
    /// Resolves the scope of a staff-app caller (<see cref="RoleNames.StaffApp"/>). Admin: every theater.
    /// Theater-scoped roles: their theater, and false (caller answers 403, never 401) when the token carries
    /// none or the role is not a staff role.
    /// </summary>
    public static bool TryGetStaffScope(this ClaimsPrincipal principal, out StaffScope scope)
    {
        scope = new StaffScope(false, Array.Empty<Guid>());
        if (principal.IsAdmin())
        {
            scope = new StaffScope(true, Array.Empty<Guid>());
            return true;
        }
        if (IsInAnyRole(principal, _theaterScopedRoles))
        {
            var theaterIds = principal.GetTheaterIds();
            scope = new StaffScope(false, theaterIds);
            return theaterIds.Count > 0;
        }
        return false;
    }
}

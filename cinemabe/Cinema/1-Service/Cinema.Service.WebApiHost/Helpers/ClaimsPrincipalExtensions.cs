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
}

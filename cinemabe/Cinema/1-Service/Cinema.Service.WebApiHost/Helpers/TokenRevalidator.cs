using System.Security.Claims;
using Cinema.Data.Contexts;
using Cinema.Data.Enums;
using Cinema.Data.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace Cinema.Service.WebApiHost.Helpers;

/// <summary>What the database says about the account a token was issued to, right now.</summary>
public sealed record UserSnapshot(UserStatus Status, string RoleName, Guid? TheaterId, string Stamp);

/// <summary>
/// A JWT is self-contained, so on its own a banned, deactivated, demoted or re-scoped account — or one whose
/// password was just changed — would keep working until the token expires. This re-checks the account against
/// the database on every authenticated request (and on every SignalR connect) and rejects a stale token.
/// </summary>
public static class TokenRevalidator
{
    /// <summary>The reason the token no longer matches the account, or null when it is still valid.</summary>
    public static string? RejectionReason(ClaimsPrincipal principal, UserSnapshot? current)
    {
        if (current == null)
        {
            return "Account no longer exists.";
        }
        if (current.Status != UserStatus.Active)
        {
            return "This account is disabled.";
        }

        var tokenRole = principal.FindFirstValue(ClaimTypes.Role) ?? principal.FindFirstValue("role");
        if (!string.Equals(tokenRole, current.RoleName, StringComparison.Ordinal))
        {
            return "Account role changed. Sign in again.";
        }

        var tokenTheater = Guid.TryParse(principal.FindFirstValue("theaterId"), out var theaterId) ? theaterId : (Guid?)null;
        if (tokenTheater != current.TheaterId)
        {
            return "Account theater changed. Sign in again.";
        }

        var tokenStamp = principal.FindFirstValue(TokenStamp.ClaimType);
        if (!string.Equals(tokenStamp, current.Stamp, StringComparison.Ordinal))
        {
            return "Credentials changed. Sign in again.";
        }

        return null;
    }

    /// <summary><see cref="JwtBearerEvents.OnTokenValidated"/> handler.</summary>
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var subject = principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal?.FindFirstValue("sub");
        if (principal == null || !Guid.TryParse(subject, out var userId))
        {
            context.Fail("Token has no user id.");
            return;
        }

        UserSnapshot? snapshot;
        if (UserSnapshotCache.TryGet(userId, out var cached) && cached is UserSnapshot hit)
        {
            snapshot = hit;
        }
        else
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<CinemaContext>();
            var row = await db.User.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.Status, Role = u.UserType.Name, u.TheaterId, u.PasswordHash, u.PasswordSalt })
                .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

            snapshot = row == null
                ? null
                : new UserSnapshot(row.Status, row.Role, row.TheaterId, TokenStamp.Compute(row.PasswordHash, row.PasswordSalt));
            if (snapshot != null)
            {
                // Missing accounts are not cached: a just-created account must be usable straight away.
                UserSnapshotCache.Set(userId, snapshot);
            }
        }

        var reason = RejectionReason(principal, snapshot);
        if (reason != null)
        {
            context.Fail(reason);
        }
    }
}

using System.Threading.RateLimiting;
using Cinema.Foundation.Logging;
using Microsoft.AspNetCore.RateLimiting;

namespace Cinema.Service.WebApiHost.Helpers;

/// <summary>
/// Rate-limit policies for the unauthenticated identity surface. Partitioned by client IP, so a
/// single caller cannot brute-force credentials or use the account as an outbound mail relay.
/// Behind a reverse proxy the partition key is the proxy's address unless forwarded headers are
/// configured — add <c>UseForwardedHeaders</c> before deploying behind one.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Credential-guessing surface: login, 2FA verification, token redemption, registration.</summary>
    public const string Auth = "auth";

    /// <summary>Endpoints that send an email on every call — throttled harder to prevent mail abuse.</summary>
    public const string AuthEmail = "auth-email";

    public static IServiceCollection AddCinemaRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString();
                }

                LogProvider.Current.Warning(
                    $"Rate limit hit on {context.HttpContext.Request.Path} by {ClientKey(context.HttpContext)}.");

                await context.HttpContext.Response.WriteAsync(
                    "Too many requests. Please wait and try again.", cancellationToken);
            };

            options.AddPolicy(Auth, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ClientKey(httpContext),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window      = TimeSpan.FromMinutes(1),
                    QueueLimit  = 0
                }));

            options.AddPolicy(AuthEmail, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ClientKey(httpContext),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 3,
                    Window      = TimeSpan.FromMinutes(15),
                    QueueLimit  = 0
                }));
        });

        return services;
    }

    private static string ClientKey(HttpContext httpContext)
    {
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

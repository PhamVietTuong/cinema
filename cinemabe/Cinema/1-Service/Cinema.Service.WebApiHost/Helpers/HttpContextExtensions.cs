using Microsoft.AspNetCore.Http;

namespace Cinema.Service.WebApiHost.Helpers;

public static class HttpContextExtensions
{
    /// <summary>The caller's IP, in IPv4 form when possible (some payment providers, e.g. VNPay, expect it).</summary>
    public static string? GetClientIp(this HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address == null)
        {
            return null;
        }
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }
        return address.ToString();
    }
}

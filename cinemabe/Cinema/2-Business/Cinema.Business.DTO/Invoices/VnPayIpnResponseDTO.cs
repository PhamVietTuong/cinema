using System.Text.Json.Serialization;

namespace Cinema.Business.DTO.Invoices;

/// <summary>VNPay's required IPN acknowledgement shape. The <see cref="JsonPropertyNameAttribute"/>s are
/// load-bearing: they override ASP.NET Core's default camelCase policy, which VNPay does not accept.</summary>
public class VnPayIpnResponseDTO
{
    [JsonPropertyName("RspCode")]
    public string RspCode { get; set; } = string.Empty;

    [JsonPropertyName("Message")]
    public string Message { get; set; } = string.Empty;
}

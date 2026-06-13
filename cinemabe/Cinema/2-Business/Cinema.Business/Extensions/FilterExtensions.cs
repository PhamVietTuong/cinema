namespace Cinema.Business.Extensions;

internal static class FilterExtensions
{
    public static string? GetString(this Dictionary<string, string>? filters, string key)
        => filters?.GetValueOrDefault(key);

    public static Guid? GetGuid(this Dictionary<string, string>? filters, string key)
        => filters != null && filters.TryGetValue(key, out var v) && Guid.TryParse(v, out var g) ? g : null;

    public static DateOnly? GetDateOnly(this Dictionary<string, string>? filters, string key)
        => filters != null && filters.TryGetValue(key, out var v) && DateOnly.TryParse(v, out var d) ? d : null;

    public static DateTime? GetDateTime(this Dictionary<string, string>? filters, string key)
        => filters != null && filters.TryGetValue(key, out var v) && DateTime.TryParse(v, out var dt) ? dt : null;

    public static TEnum? GetEnum<TEnum>(this Dictionary<string, string>? filters, string key) where TEnum : struct, Enum
        => filters != null && filters.TryGetValue(key, out var v) && Enum.TryParse<TEnum>(v, out var e) ? e : null;

    public static bool? GetBool(this Dictionary<string, string>? filters, string key)
        => filters != null && filters.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : null;
}

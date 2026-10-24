using System.Text.Json;
using Cinema.Business.Contracts;
using Cinema.Business.DTO.Movies;
using Cinema.Foundation.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace Cinema.Business.ExternalRatings;

/// <summary>
/// Looks up Rotten Tomatoes/IMDb/Metascore review data from the OMDb API (http://www.omdbapi.com),
/// matching by title + release year since OMDb has no "search by local movie id" endpoint.
///
/// Config under <c>OMDb:ApiKey</c>. Results are cached in-process (not persisted): a found result for
/// 24h, a "not found" result for 6h (so repeated misses for the same title don't keep calling OMDb),
/// and a transient failure for 10 minutes before retrying. Never throws — every failure path (missing
/// config, bad input, non-2xx, timeout, malformed/unexpected-shape JSON) returns null so callers can
/// treat "no data" and "couldn't fetch" the same way.
/// </summary>
public class OmdbRatingsProvider : IExternalRatingsProvider
{
    public const string HttpClientName = "omdb";

    private const string ApiKeyConfigKey = "OMDb:ApiKey";
    private const string RottenTomatoesSource = "Rotten Tomatoes";
    private const string NotAvailable = "N/A";

    private static readonly TimeSpan FoundTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan NotFoundTtl = TimeSpan.FromHours(6);
    private static readonly TimeSpan FailureTtl = TimeSpan.FromMinutes(10);

    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IMemoryCache _cache;

    /// <summary>Distinguishes "cached as not found" (Ratings is null) from "never looked up" (cache miss).</summary>
    private sealed record CacheEntry(ExternalRatingsDTO? Ratings);

    public OmdbRatingsProvider(IConfiguration config, IHttpClientFactory httpFactory, IMemoryCache cache)
    {
        _config = config;
        _httpFactory = httpFactory;
        _cache = cache;
    }

    private string GetApiKey()
    {
        var apiKey = _config[ApiKeyConfigKey];
        if (apiKey == null)
        {
            return "";
        }
        return apiKey;
    }

    public async Task<ExternalRatingsDTO?> GetRatingsAsync(string title, int year, CancellationToken cancellationToken = default)
    {
        string trimmedTitle;
        if (title == null)
        {
            trimmedTitle = "";
        }
        else
        {
            trimmedTitle = title.Trim();
        }
        if (trimmedTitle.Length == 0 || year <= 0)
        {
            return null;
        }

        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var cacheKey = $"omdb:{trimmedTitle.ToLowerInvariant()}:{year}";
        if (_cache.TryGetValue(cacheKey, out CacheEntry? cached) && cached != null)
        {
            return cached.Ratings;
        }

        try
        {
            var http = _httpFactory.CreateClient(HttpClientName);
            var url = $"/?apikey={Uri.EscapeDataString(apiKey)}&t={Uri.EscapeDataString(trimmedTitle)}&y={year}&type=movie";
            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                CacheFailure(cacheKey, trimmedTitle, year, $"HTTP {(int)response.StatusCode}", null);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var omdbResponse = GetStringProperty(root, "Response");

            if (!string.Equals(omdbResponse, "True", StringComparison.OrdinalIgnoreCase))
            {
                var error = GetStringProperty(root, "Error");
                if (error == null)
                {
                    error = "";
                }
                if (error.Contains("key", StringComparison.OrdinalIgnoreCase) ||
                    error.Contains("limit", StringComparison.OrdinalIgnoreCase))
                {
                    CacheFailure(cacheKey, trimmedTitle, year, error, null);
                    return null;
                }
                _cache.Set(cacheKey, new CacheEntry(null), NotFoundTtl);
                return null;
            }

            var ratings = BuildRatings(root);
            _cache.Set(cacheKey, new CacheEntry(ratings), FoundTtl);
            return ratings;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            CacheFailure(cacheKey, trimmedTitle, year, "timeout", null);
            return null;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Every other failure (bad HTTP, malformed JSON, or an unexpected response shape such as
            // a non-string field) must still honor the "never throws" contract. Never log the request
            // URL anywhere here: it carries the OMDb API key.
            CacheFailure(cacheKey, trimmedTitle, year, e.Message, e);
            return null;
        }
    }

    private void CacheFailure(string cacheKey, string title, int year, string reason, Exception? exception)
    {
        if (exception == null)
        {
            LogProvider.Current.Warning($"OmdbRatingsProvider.GetRatingsAsync failed for '{title}' ({year}): {reason}");
        }
        else
        {
            LogProvider.Current.Warning(exception, $"OmdbRatingsProvider.GetRatingsAsync failed for '{title}' ({year}): {reason}");
        }
        _cache.Set(cacheKey, new CacheEntry(null), FailureTtl);
    }

    private static ExternalRatingsDTO? BuildRatings(JsonElement root)
    {
        string? rottenTomatoes = null;
        if (root.TryGetProperty("Ratings", out var ratingsArray) && ratingsArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in ratingsArray.EnumerateArray())
            {
                var source = GetStringProperty(entry, "Source");
                if (string.Equals(source, RottenTomatoesSource, StringComparison.OrdinalIgnoreCase))
                {
                    rottenTomatoes = NormalizeValue(GetStringProperty(entry, "Value"));
                    break;
                }
            }
        }

        var imdbRating = NormalizeValue(GetStringProperty(root, "imdbRating"));
        var metascore = NormalizeValue(GetStringProperty(root, "Metascore"));

        if (rottenTomatoes == null && imdbRating == null && metascore == null)
        {
            return null;
        }

        return new ExternalRatingsDTO
        {
            RottenTomatoes = rottenTomatoes,
            ImdbRating = imdbRating,
            Metascore = metascore,
        };
    }

    /// <summary>Reads a string property off a JSON object, tolerating a missing property or an
    /// unexpected shape (e.g. OMDb/a proxy returning a number, bool, or non-object) by returning
    /// null instead of throwing.</summary>
    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }
        if (value.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        return value.GetString();
    }

    private static string? NormalizeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == NotAvailable)
        {
            return null;
        }
        return value;
    }
}

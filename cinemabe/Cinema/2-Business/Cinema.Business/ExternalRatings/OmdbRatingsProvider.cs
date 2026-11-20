using System.Text.Json;
using Cinema.Business.Contracts;
using Cinema.Business.DTO.Movies;
using Cinema.Foundation.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace Cinema.Business.ExternalRatings;

/// <summary>
/// Looks up Rotten Tomatoes/IMDb/Metascore review data from the OMDb API (http://www.omdbapi.com),
/// by IMDb id when one is stored on the movie (exact match), otherwise by title + release year
/// (best-effort, since titles that don't exactly match OMDb's catalog — e.g. a Vietnamese title —
/// simply miss).
///
/// Config under <c>OMDb:ApiKey</c>. Results are cached in-process (not persisted): a found result for
/// 24h, a "not found" result for 6h (so repeated misses for the same lookup don't keep calling OMDb),
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

    public Task<ExternalRatingsDTO?> GetRatingsAsync(string title, int year, string? imdbId, CancellationToken cancellationToken = default)
    {
        string trimmedImdbId;
        if (imdbId == null)
        {
            trimmedImdbId = "";
        }
        else
        {
            trimmedImdbId = imdbId.Trim();
        }

        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Task.FromResult<ExternalRatingsDTO?>(null);
        }

        if (trimmedImdbId.Length > 0)
        {
            var cacheKey = $"omdb:imdb:{trimmedImdbId.ToLowerInvariant()}";
            var url = $"/?apikey={Uri.EscapeDataString(apiKey)}&i={Uri.EscapeDataString(trimmedImdbId)}&type=movie";
            return FetchAsync(url, cacheKey, $"imdb {trimmedImdbId}", cancellationToken);
        }

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
            return Task.FromResult<ExternalRatingsDTO?>(null);
        }

        var titleCacheKey = $"omdb:title:{trimmedTitle.ToLowerInvariant()}:{year}";
        var titleUrl = $"/?apikey={Uri.EscapeDataString(apiKey)}&t={Uri.EscapeDataString(trimmedTitle)}&y={year}&type=movie";
        return FetchAsync(titleUrl, titleCacheKey, $"'{trimmedTitle}' ({year})", cancellationToken);
    }

    /// <summary>Title search via OMDb <c>?s=</c> (max 10 hits, first page). Same caching/never-throws
    /// rules as <see cref="GetRatingsAsync"/>: a hit list is cached 24h, "not found" 6h, a failure 10 min.</summary>
    public async Task<IReadOnlyList<ImdbMatchDTO>> SearchImdbAsync(string title, int? year, CancellationToken cancellationToken = default)
    {
        var empty = new List<ImdbMatchDTO>();

        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return empty;
        }

        string trimmedTitle;
        if (title == null)
        {
            trimmedTitle = "";
        }
        else
        {
            trimmedTitle = title.Trim();
        }
        if (trimmedTitle.Length == 0)
        {
            return empty;
        }

        var url = $"/?apikey={Uri.EscapeDataString(apiKey)}&s={Uri.EscapeDataString(trimmedTitle)}&type=movie";
        var cacheKey = $"omdb:search:{trimmedTitle.ToLowerInvariant()}";
        if (year.HasValue && year.Value > 0)
        {
            url += $"&y={year.Value}";
            cacheKey += $":{year.Value}";
        }
        var lookupLabel = $"search '{trimmedTitle}'";

        if (_cache.TryGetValue(cacheKey, out List<ImdbMatchDTO>? cached) && cached != null)
        {
            return cached;
        }

        try
        {
            var http = _httpFactory.CreateClient(HttpClientName);
            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                CacheSearchFailure(cacheKey, lookupLabel, $"HTTP {(int)response.StatusCode}", null);
                return empty;
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
                    CacheSearchFailure(cacheKey, lookupLabel, error, null);
                    return empty;
                }
                _cache.Set(cacheKey, empty, NotFoundTtl);
                return empty;
            }

            var matches = BuildMatches(root);
            _cache.Set(cacheKey, matches, FoundTtl);
            return matches;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            CacheSearchFailure(cacheKey, lookupLabel, "timeout", null);
            return empty;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            CacheSearchFailure(cacheKey, lookupLabel, e.Message, e);
            return empty;
        }
    }

    private void CacheSearchFailure(string cacheKey, string lookupLabel, string reason, Exception? exception)
    {
        if (exception == null)
        {
            LogProvider.Current.Warning($"OmdbRatingsProvider.SearchImdbAsync failed for {lookupLabel}: {reason}");
        }
        else
        {
            LogProvider.Current.Warning(exception, $"OmdbRatingsProvider.SearchImdbAsync failed for {lookupLabel}: {reason}");
        }
        _cache.Set(cacheKey, new List<ImdbMatchDTO>(), FailureTtl);
    }

    private static List<ImdbMatchDTO> BuildMatches(JsonElement root)
    {
        var matches = new List<ImdbMatchDTO>();
        if (!root.TryGetProperty("Search", out var searchArray) || searchArray.ValueKind != JsonValueKind.Array)
        {
            return matches;
        }

        foreach (var entry in searchArray.EnumerateArray())
        {
            var imdbId = NormalizeValue(GetStringProperty(entry, "imdbID"));
            var matchTitle = NormalizeValue(GetStringProperty(entry, "Title"));
            if (imdbId == null || matchTitle == null)
            {
                continue;
            }

            var matchYear = NormalizeValue(GetStringProperty(entry, "Year"));
            if (matchYear == null)
            {
                matchYear = "";
            }

            matches.Add(new ImdbMatchDTO
            {
                ImdbId = imdbId,
                Title = matchTitle,
                Year = matchYear,
                PosterUrl = NormalizeValue(GetStringProperty(entry, "Poster")),
            });
        }
        return matches;
    }

    private async Task<ExternalRatingsDTO?> FetchAsync(string relativeUrl, string cacheKey, string lookupLabel, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(cacheKey, out CacheEntry? cached) && cached != null)
        {
            return cached.Ratings;
        }

        try
        {
            var http = _httpFactory.CreateClient(HttpClientName);
            using var response = await http.GetAsync(relativeUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                CacheFailure(cacheKey, lookupLabel, $"HTTP {(int)response.StatusCode}", null);
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
                    CacheFailure(cacheKey, lookupLabel, error, null);
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
            CacheFailure(cacheKey, lookupLabel, "timeout", null);
            return null;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Every other failure (bad HTTP, malformed JSON, or an unexpected response shape such as
            // a non-string field) must still honor the "never throws" contract. Never log the request
            // URL anywhere here: it carries the OMDb API key.
            CacheFailure(cacheKey, lookupLabel, e.Message, e);
            return null;
        }
    }

    private void CacheFailure(string cacheKey, string lookupLabel, string reason, Exception? exception)
    {
        if (exception == null)
        {
            LogProvider.Current.Warning($"OmdbRatingsProvider.GetRatingsAsync failed for {lookupLabel}: {reason}");
        }
        else
        {
            LogProvider.Current.Warning(exception, $"OmdbRatingsProvider.GetRatingsAsync failed for {lookupLabel}: {reason}");
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

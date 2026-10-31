using System.Net;
using Cinema.Business.ExternalRatings;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Cinema.Business.Tests;

public class OmdbRatingsProviderTests
{
    private const string ApiKey = "test-key";

    [Fact]
    public async Task GetRatingsAsync_FullPayload_ReturnsAllThreeScores()
    {
        var (sut, handler) = CreateSut(ApiKey, Respond(HttpStatusCode.OK, FullPayloadJson()));

        var result = await sut.GetRatingsAsync("Inception", 2010, null);

        result.Should().NotBeNull();
        result!.RottenTomatoes.Should().Be("87%");
        result.ImdbRating.Should().Be("8.8");
        result.Metascore.Should().Be("74");
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetRatingsAsync_NoRottenTomatoesEntry_ImdbStillSet()
    {
        const string json = """
            {"Response":"True","imdbRating":"7.5","Metascore":"60","Ratings":[{"Source":"Internet Movie Database","Value":"7.5/10"}]}
            """;
        var (sut, _) = CreateSut(ApiKey, Respond(HttpStatusCode.OK, json));

        var result = await sut.GetRatingsAsync("Some Movie", 2020, null);

        result.Should().NotBeNull();
        result!.RottenTomatoes.Should().BeNull();
        result.ImdbRating.Should().Be("7.5");
    }

    [Fact]
    public async Task GetRatingsAsync_NotAvailableValues_BecomeNull()
    {
        const string json = """
            {"Response":"True","imdbRating":"N/A","Metascore":"N/A","Ratings":[]}
            """;
        var (sut, _) = CreateSut(ApiKey, Respond(HttpStatusCode.OK, json));

        var result = await sut.GetRatingsAsync("Obscure Movie", 2020, null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRatingsAsync_MovieNotFound_ReturnsNull_AndNegativeCaches()
    {
        const string json = """{"Response":"False","Error":"Movie not found!"}""";
        var (sut, handler) = CreateSut(ApiKey, Respond(HttpStatusCode.OK, json));

        var first  = await sut.GetRatingsAsync("Unknown Movie", 2020, null);
        var second = await sut.GetRatingsAsync("Unknown Movie", 2020, null);

        first.Should().BeNull();
        second.Should().BeNull();
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetRatingsAsync_InvalidApiKeyError_ReturnsNull_AndCachesAsFailure()
    {
        const string json = """{"Response":"False","Error":"Invalid API key!"}""";
        var (sut, handler) = CreateSut(ApiKey, Respond(HttpStatusCode.OK, json));

        var first  = await sut.GetRatingsAsync("Any Movie", 2020, null);
        var second = await sut.GetRatingsAsync("Any Movie", 2020, null);

        first.Should().BeNull();
        second.Should().BeNull();
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetRatingsAsync_ResponseFieldIsNotAString_ReturnsNull_DoesNotThrow()
    {
        const string json = """{"Response":true,"Ratings":[]}""";
        var (sut, _) = CreateSut(ApiKey, Respond(HttpStatusCode.OK, json));

        var result = await sut.GetRatingsAsync("Any Movie", 2020, null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRatingsAsync_RatingsEntryIsNotAnObject_ReturnsNull_DoesNotThrow()
    {
        const string json = """{"Response":"True","imdbRating":"7.0","Ratings":["not-an-object"]}""";
        var (sut, _) = CreateSut(ApiKey, Respond(HttpStatusCode.OK, json));

        var result = await sut.GetRatingsAsync("Any Movie", 2020, null);

        result.Should().NotBeNull();
        result!.ImdbRating.Should().Be("7.0");
        result.RottenTomatoes.Should().BeNull();
    }

    [Fact]
    public async Task GetRatingsAsync_TopLevelResponseIsAnArray_ReturnsNull_DoesNotThrow()
    {
        var (sut, _) = CreateSut(ApiKey, Respond(HttpStatusCode.OK, "[]"));

        var result = await sut.GetRatingsAsync("Any Movie", 2020, null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRatingsAsync_HttpServerError_ReturnsNull_DoesNotThrow()
    {
        var (sut, _) = CreateSut(ApiKey, Respond(HttpStatusCode.InternalServerError, ""));

        var result = await sut.GetRatingsAsync("Any Movie", 2020, null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRatingsAsync_MalformedJson_ReturnsNull_DoesNotThrow()
    {
        var (sut, _) = CreateSut(ApiKey, Respond(HttpStatusCode.OK, "{not json"));

        var result = await sut.GetRatingsAsync("Any Movie", 2020, null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRatingsAsync_HandlerThrows_ReturnsNull_DoesNotThrow()
    {
        var (sut, _) = CreateSut(ApiKey, new ThrowingHandler());

        var result = await sut.GetRatingsAsync("Any Movie", 2020, null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRatingsAsync_BlankApiKey_ReturnsNull_MakesNoHttpCall()
    {
        var (sut, handler) = CreateSut("", Respond(HttpStatusCode.OK, FullPayloadJson()));

        var result = await sut.GetRatingsAsync("Inception", 2010, null);

        result.Should().BeNull();
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task GetRatingsAsync_SameTitleDifferentCaseAndWhitespace_MakesOneHttpCall()
    {
        var (sut, handler) = CreateSut(ApiKey, Respond(HttpStatusCode.OK, FullPayloadJson()));

        await sut.GetRatingsAsync("Inception", 2010, null);
        await sut.GetRatingsAsync("  INCEPTION  ", 2010, null);

        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetRatingsAsync_RequestUrl_ContainsTitleYearAndKey()
    {
        var (sut, handler) = CreateSut(ApiKey, Respond(HttpStatusCode.OK, FullPayloadJson()));

        await sut.GetRatingsAsync("The Matrix", 1999, null);

        handler.LastRequestUri.Should().NotBeNull();
        var query = handler.LastRequestUri!.Query;
        query.Should().Contain("t=The%20Matrix");
        query.Should().Contain("y=1999");
        query.Should().Contain($"apikey={ApiKey}");
    }

    [Fact]
    public async Task GetRatingsAsync_WithImdbId_QueriesByIdNotTitle()
    {
        var (sut, handler) = CreateSut(ApiKey, Respond(HttpStatusCode.OK, FullPayloadJson()));

        await sut.GetRatingsAsync("Inception", 2010, "tt0133093");

        var query = handler.LastRequestUri!.Query;
        query.Should().Contain("i=tt0133093");
        query.Should().Contain($"apikey={ApiKey}");
        query.Should().NotContain("&t=");
        query.Should().NotContain("&y=");
    }

    [Fact]
    public async Task GetRatingsAsync_WithImdbId_IgnoresBlankTitleAndZeroYear()
    {
        var (sut, handler) = CreateSut(ApiKey, Respond(HttpStatusCode.OK, FullPayloadJson()));

        var result = await sut.GetRatingsAsync("", 0, "tt0133093");

        result.Should().NotBeNull();
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetRatingsAsync_WhitespaceImdbId_FallsBackToTitleYear()
    {
        var (sut, handler) = CreateSut(ApiKey, Respond(HttpStatusCode.OK, FullPayloadJson()));

        await sut.GetRatingsAsync("Inception", 2010, "   ");

        var query = handler.LastRequestUri!.Query;
        query.Should().Contain("t=Inception");
        query.Should().Contain("y=2010");
    }

    [Fact]
    public async Task GetRatingsAsync_WithImdbId_NotFound_NegativeCaches_NoTitleFallback()
    {
        const string json = """{"Response":"False","Error":"Movie not found!"}""";
        var (sut, handler) = CreateSut(ApiKey, Respond(HttpStatusCode.OK, json));

        var first  = await sut.GetRatingsAsync("Inception", 2010, "tt9999999");
        var second = await sut.GetRatingsAsync("Inception", 2010, "tt9999999");

        first.Should().BeNull();
        second.Should().BeNull();
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetRatingsAsync_SameImdbIdDifferentCase_MakesOneHttpCall()
    {
        var (sut, handler) = CreateSut(ApiKey, Respond(HttpStatusCode.OK, FullPayloadJson()));

        await sut.GetRatingsAsync("Inception", 2010, "tt0133093");
        await sut.GetRatingsAsync("Inception", 2010, "TT0133093");

        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetRatingsAsync_ImdbIdAndTitleLookups_UseSeparateCacheEntries()
    {
        var (sut, handler) = CreateSut(ApiKey, Respond(HttpStatusCode.OK, FullPayloadJson()));

        await sut.GetRatingsAsync("Inception", 2010, null);
        await sut.GetRatingsAsync("Inception", 2010, "tt0133093");

        handler.CallCount.Should().Be(2);
    }

    private static string FullPayloadJson()
    {
        return """
            {"Response":"True","imdbRating":"8.8","Metascore":"74","Ratings":[
              {"Source":"Internet Movie Database","Value":"8.8/10"},
              {"Source":"Rotten Tomatoes","Value":"87%"},
              {"Source":"Metacritic","Value":"74/100"}
            ]}
            """;
    }

    private static (OmdbRatingsProvider Sut, FakeHandler Handler) CreateSut(string apiKey, HttpMessageHandler handler)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OMDb:ApiKey"] = apiKey })
            .Build();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://www.omdbapi.com") };
        var httpFactoryMock = new Mock<IHttpClientFactory>();
        httpFactoryMock.Setup(f => f.CreateClient(OmdbRatingsProvider.HttpClientName)).Returns(httpClient);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new OmdbRatingsProvider(config, httpFactoryMock.Object, cache);
        return (sut, (handler as FakeHandler)!);
    }

    private static FakeHandler Respond(HttpStatusCode statusCode, string body)
    {
        return new FakeHandler(statusCode, body);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;

        public int CallCount { get; private set; }
        public Uri? LastRequestUri { get; private set; }

        public FakeHandler(HttpStatusCode statusCode, string body)
        {
            _statusCode = statusCode;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequestUri = request.RequestUri;
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body),
            };
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Connection failed.");
        }
    }
}

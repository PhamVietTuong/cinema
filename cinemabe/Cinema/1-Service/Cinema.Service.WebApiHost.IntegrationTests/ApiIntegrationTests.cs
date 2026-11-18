using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Cinema.Service.WebApiHost.IntegrationTests;

/// <summary>One real API + one real SQL Server database shared by every test in the class (xUnit class fixture).</summary>
public sealed class ApiIntegrationTests : IClassFixture<ApiIntegrationTests.Fixture>
{
    public sealed class Fixture : IDisposable
    {
        public CinemaApiFactory? Factory { get; } =
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(RequiresSqlServerFactAttribute.EnvironmentVariable))
                ? null
                : new CinemaApiFactory();

        public void Dispose()
        {
            Factory?.Dispose();
        }
    }

    private static readonly object Paging = new { pageIndex = 1, pageSize = 5 };
    private readonly HttpClient _client;

    public ApiIntegrationTests(Fixture fixture)
    {
        _client = fixture.Factory?.CreateClient() ?? new HttpClient();
    }

    private static object RegisterBody(string email, string phone, string password)
    {
        return new { name = "Integration Tester", email, phone, password, confirmPassword = password };
    }

    [RequiresSqlServerFact]
    public async Task Readiness_checks_the_database()
    {
        var response = await _client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [RequiresSqlServerFact]
    public async Task Public_catalog_queries_translate_to_SQL_on_the_committed_schema()
    {
        foreach (var action in new[] { "GetMovies", "GetNowShowingMovies", "GetComingSoonMovies", "GetTheaters", "GetShowTimes" })
        {
            var response = await _client.PostAsJsonAsync("/api/Cinema/" + action, Paging);

            response.StatusCode.Should().Be(HttpStatusCode.OK, action + " must run against create_db.sql's schema");
        }

        // insert_db.sql seeds a catalog: an empty result would mean the query silently filtered everything out.
        var movies = await (await _client.PostAsJsonAsync("/api/Cinema/GetMovies", Paging)).Content.ReadFromJsonAsync<JsonElement>();
        movies.GetProperty("results").GetArrayLength().Should().BeGreaterThan(0);
    }

    [RequiresSqlServerFact]
    public async Task Admin_endpoints_reject_anonymous_callers_through_the_real_pipeline()
    {
        var response = await _client.PostAsJsonAsync("/api/Identity/GetUsers", Paging);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [RequiresSqlServerFact]
    public async Task Seeded_customer_can_login_and_read_own_invoices_through_the_database()
    {
        var login = await _client.PostAsJsonAsync("/api/Identity/Login", new { emailOrPhone = "khach01@cinema.vn", password = "Khach01@123" });
        login.StatusCode.Should().Be(HttpStatusCode.OK, await login.Content.ReadAsStringAsync());
        var auth = await login.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        var token = auth!.First(kv => kv.Key.Equals("token", StringComparison.OrdinalIgnoreCase)).Value.GetString();
        token.Should().NotBeNullOrEmpty();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Payment/GetMyInvoices") { Content = JsonContent.Create(Paging) };
        request.Headers.Authorization = new("Bearer", token);
        var invoices = await _client.SendAsync(request);

        invoices.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [RequiresSqlServerFact]
    public async Task New_account_cannot_sign_in_until_its_email_is_verified()
    {
        var email = "it-" + Guid.NewGuid().ToString("N") + "@cinema.test";
        const string password = "IntegrationPwd1!";

        var register = await _client.PostAsJsonAsync("/api/Identity/Register", RegisterBody(email, "0900000" + Random.Shared.Next(100, 999), password));
        register.StatusCode.Should().Be(HttpStatusCode.OK, await register.Content.ReadAsStringAsync());

        var login = await _client.PostAsJsonAsync("/api/Identity/Login", new { emailOrPhone = email, password });

        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await login.Content.ReadAsStringAsync()).Should().Contain("verify your email");
    }

    [RequiresSqlServerFact]
    public async Task Duplicate_registration_is_refused_not_a_server_error()
    {
        var email = "it-" + Guid.NewGuid().ToString("N") + "@cinema.test";

        (await _client.PostAsJsonAsync("/api/Identity/Register", RegisterBody(email, "0911111111", "IntegrationPwd1!"))).StatusCode
            .Should().Be(HttpStatusCode.OK);
        var second = await _client.PostAsJsonAsync("/api/Identity/Register", RegisterBody(email, "0922222222", "IntegrationPwd1!"));

        ((int)second.StatusCode).Should().BeInRange(400, 499);
    }

    [RequiresSqlServerFact]
    public async Task Unsigned_payment_callback_is_answered_in_the_IPN_shape_and_never_marks_paid()
    {
        var response = await _client.GetAsync("/api/Payment/PaymentCallback?provider=VNPay&vnp_TxnRef=it-fake&vnp_ResponseCode=00&vnp_SecureHash=bad");

        ((int)response.StatusCode).Should().BeLessThan(500);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("RspCode").And.NotContain("\"00\"");
        }
    }
}

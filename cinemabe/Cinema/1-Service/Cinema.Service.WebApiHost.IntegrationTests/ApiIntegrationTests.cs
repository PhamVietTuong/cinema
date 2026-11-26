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

    // A token that is validly signed but no longer matches the account must be refused (TokenRevalidator).
    private async Task<System.IdentityModel.Tokens.Jwt.JwtSecurityToken> LoginAsSeededCustomerAsync()
    {
        var login = await _client.PostAsJsonAsync("/api/Identity/Login", new { emailOrPhone = "khach01@cinema.vn", password = "Khach01@123" });
        login.StatusCode.Should().Be(HttpStatusCode.OK, await login.Content.ReadAsStringAsync());
        var auth = await login.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        var token = auth!.First(kv => kv.Key.Equals("token", StringComparison.OrdinalIgnoreCase)).Value.GetString();
        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);
    }

    private static string ResignedToken(System.IdentityModel.Tokens.Jwt.JwtSecurityToken original, Func<System.Security.Claims.Claim, System.Security.Claims.Claim?> rewrite)
    {
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes("integration-tests-only-" + new string('x', 48)));
        var claims = original.Claims.Select(rewrite).Where(c => c != null).Cast<System.Security.Claims.Claim>().ToList();
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: original.Issuer,
            audience: original.Audiences.First(),
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));
        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<HttpStatusCode> CallWithAsync(string token, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(Paging) };
        request.Headers.Authorization = new("Bearer", token);
        return (await _client.SendAsync(request)).StatusCode;
    }

    [RequiresSqlServerFact]
    public async Task Validly_signed_token_with_an_inflated_role_is_refused()
    {
        var original = await LoginAsSeededCustomerAsync();
        var elevated = ResignedToken(original, c => c.Type is "role" or System.Security.Claims.ClaimTypes.Role
            ? new System.Security.Claims.Claim(c.Type, "Admin")
            : c);

        (await CallWithAsync(elevated, "/api/Identity/GetUsers")).Should().Be(HttpStatusCode.Unauthorized);
    }

    [RequiresSqlServerFact]
    public async Task Validly_signed_token_without_the_credential_stamp_is_refused()
    {
        var original = await LoginAsSeededCustomerAsync();
        // Control: the same claims re-signed with the test key are accepted, so a 401 below can only be the stamp.
        (await CallWithAsync(ResignedToken(original, c => c), "/api/Payment/GetMyInvoices")).Should().Be(HttpStatusCode.OK);
        var stampless = ResignedToken(original, c => c.Type == "stamp" ? null : c);

        (await CallWithAsync(stampless, "/api/Payment/GetMyInvoices")).Should().Be(HttpStatusCode.Unauthorized);
    }

    [RequiresSqlServerFact]
    public async Task Registration_opens_no_session_until_the_email_is_confirmed()
    {
        var email = "it-" + Guid.NewGuid().ToString("N") + "@cinema.test";

        var register = await _client.PostAsJsonAsync("/api/Identity/Register", RegisterBody(email, "0933" + Random.Shared.Next(100000, 999999), "IntegrationPwd1!"));

        register.StatusCode.Should().Be(HttpStatusCode.OK, await register.Content.ReadAsStringAsync());
        var body = await register.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        body!.First(kv => kv.Key.Equals("token", StringComparison.OrdinalIgnoreCase)).Value.GetString().Should().BeNullOrEmpty();
    }

    [RequiresSqlServerFact]
    public async Task Responses_carry_the_baseline_security_headers()
    {
        var response = await _client.GetAsync("/health");

        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().ContainSingle("DENY");
    }

    [RequiresSqlServerFact]
    public async Task Anonymous_news_list_never_returns_drafts()
    {
        var response = await _client.PostAsJsonAsync("/api/Cinema/GetNewsList", new { pageIndex = 1, pageSize = 100, filters = new Dictionary<string, string> { ["isPublished"] = "false" } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("results").GetArrayLength().Should().Be(0, "an anonymous caller asked for drafts and must get none");
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

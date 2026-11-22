using System.Security.Claims;
using Cinema.Data.Enums;
using Cinema.Data.Services;
using Cinema.Service.WebApiHost.Helpers;
using FluentAssertions;

namespace Cinema.Service.WebApiHost.Tests;

public class TokenRevalidatorTests
{
    private static readonly byte[] _hash = { 1, 2, 3 };
    private static readonly byte[] _salt = { 9, 8, 7 };
    private static readonly Guid _theater = Guid.NewGuid();

    private static ClaimsPrincipal Principal(string role = "TheaterStaff", Guid? theater = null, string? stamp = null, bool withStamp = true)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        if (theater != null)
        {
            claims.Add(new Claim("theaterId", theater.Value.ToString()));
        }
        if (withStamp)
        {
            claims.Add(new Claim(TokenStamp.ClaimType, stamp ?? TokenStamp.Compute(_hash, _salt)));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static UserSnapshot Snapshot(UserStatus status = UserStatus.Active, string role = "TheaterStaff", Guid? theater = null)
        => new(status, role, theater, TokenStamp.Compute(_hash, _salt));

    [Fact]
    public void UnchangedActiveAccount_IsAccepted()
    {
        TokenRevalidator.RejectionReason(Principal(theater: _theater), Snapshot(theater: _theater)).Should().BeNull();
    }

    [Fact]
    public void DeletedAccount_IsRejected()
    {
        TokenRevalidator.RejectionReason(Principal(), null).Should().NotBeNull();
    }

    [Theory]
    [InlineData(UserStatus.Inactive)]
    [InlineData(UserStatus.Banned)]
    public void DisabledAccount_IsRejected(UserStatus status)
    {
        TokenRevalidator.RejectionReason(Principal(), Snapshot(status)).Should().Contain("disabled");
    }

    [Fact]
    public void DemotedAccount_IsRejected()
    {
        TokenRevalidator.RejectionReason(Principal(role: "Admin"), Snapshot(role: "Customer")).Should().Contain("role");
    }

    [Fact]
    public void ReassignedTheater_IsRejected()
    {
        TokenRevalidator.RejectionReason(Principal(theater: _theater), Snapshot(theater: Guid.NewGuid())).Should().Contain("theater");
    }

    [Fact]
    public void TheaterRemoved_IsRejected()
    {
        TokenRevalidator.RejectionReason(Principal(theater: _theater), Snapshot(theater: null)).Should().Contain("theater");
    }

    [Fact]
    public void PasswordChanged_InvalidatesOlderTokens()
    {
        var current = Snapshot() with { Stamp = TokenStamp.Compute(new byte[] { 4, 5, 6 }, _salt) };

        TokenRevalidator.RejectionReason(Principal(), current).Should().Contain("Credentials");
    }

    [Fact]
    public void TokenWithoutStamp_IsRejected()
    {
        TokenRevalidator.RejectionReason(Principal(withStamp: false), Snapshot()).Should().Contain("Credentials");
    }

    [Fact]
    public void Stamp_DependsOnBothHashAndSalt()
    {
        TokenStamp.Compute(_hash, _salt).Should().NotBe(TokenStamp.Compute(_hash, new byte[] { 0 }));
        TokenStamp.Compute(_hash, _salt).Should().NotBe(TokenStamp.Compute(new byte[] { 0 }, _salt));
        TokenStamp.Compute(_hash, _salt).Should().Be(TokenStamp.Compute(_hash, _salt));
    }
}

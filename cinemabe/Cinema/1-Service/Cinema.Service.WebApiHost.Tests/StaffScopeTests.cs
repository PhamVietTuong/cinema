using System.Security.Claims;
using Cinema.Business.Contracts.Exceptions;
using Cinema.Business.DTO.Auth;
using Cinema.Service.WebApiHost.Controllers;
using Cinema.Service.WebApiHost.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cinema.Service.WebApiHost.Tests;

public class StaffScopeTests
{
    private readonly Guid _theaterA = Guid.NewGuid();
    private readonly Guid _theaterB = Guid.NewGuid();

    private static ClaimsPrincipal Principal(string role, params Guid[] theaterIds)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role)
        };
        claims.AddRange(theaterIds.Select(id => new Claim("theaterId", id.ToString())));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    // ── Warehouse (back-office) scope ────────────────────────────────────────

    [Theory]
    [InlineData(RoleNames.TheaterStaff)]
    [InlineData(RoleNames.TheaterManager)]
    [InlineData(RoleNames.KitchenStaff)]
    public void BackOfficeScope_StaffAndKitchen_ArePinnedToTheirTheater(string role)
    {
        var ok = Principal(role, _theaterA).TryGetBackOfficeScope(out var scope);

        ok.Should().BeTrue();
        scope.Should().Be(_theaterA);
    }

    [Theory]
    [InlineData(RoleNames.GateStaff)]
    [InlineData(RoleNames.BoxOfficeStaff)]
    [InlineData(RoleNames.Customer)]
    public void BackOfficeScope_GateBoxOfficeAndCustomers_AreRefused(string role)
    {
        Principal(role, _theaterA).TryGetBackOfficeScope(out _).Should().BeFalse();
    }

    [Fact]
    public void BackOfficeScope_AdminSeesEveryTheater()
    {
        var ok = Principal(RoleNames.Admin).TryGetBackOfficeScope(out var scope);

        ok.Should().BeTrue();
        scope.Should().BeNull();
    }

    [Fact]
    public void BackOfficeScope_KitchenWithoutTheaterClaim_IsRefused()
    {
        Principal(RoleNames.KitchenStaff).TryGetBackOfficeScope(out _).Should().BeFalse();
    }

    // ── Staff scope ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(RoleNames.TheaterStaff)]
    [InlineData(RoleNames.TheaterManager)]
    [InlineData(RoleNames.BoxOfficeStaff)]
    [InlineData(RoleNames.GateStaff)]
    [InlineData(RoleNames.KitchenStaff)]
    public void StaffScope_TheaterRoles_GetTheirTheater_AndOmittedTheaterDefaultsToIt(string role)
    {
        var ok = Principal(role, _theaterA).TryGetStaffScope(out var scope);

        ok.Should().BeTrue();
        scope.IsAll.Should().BeFalse();
        scope.TheaterIds.Should().BeEquivalentTo(new[] { _theaterA });
        scope.Resolve(null).Should().Be(_theaterA);
        scope.Resolve(_theaterA).Should().Be(_theaterA);
    }

    [Fact]
    public void StaffScope_TheaterRoleWithoutTheaterClaim_IsRefused()
    {
        Principal(RoleNames.GateStaff).TryGetStaffScope(out _).Should().BeFalse();
    }

    [Fact]
    public void StaffScope_Customer_IsRefused()
    {
        Principal(RoleNames.Customer).TryGetStaffScope(out _).Should().BeFalse();
    }

    [Fact]
    public void StaffScope_OutOfScopeTheater_ThrowsAccessDenied()
    {
        Principal(RoleNames.BoxOfficeStaff, _theaterA).TryGetStaffScope(out var scope);

        var act = () => scope.Resolve(_theaterB);

        act.Should().Throw<AccessDeniedException>();
    }

    [Fact]
    public void StaffScope_Admin_MustPassATheater_AndMayPickAny()
    {
        Principal(RoleNames.Admin).TryGetStaffScope(out var scope);

        scope.IsAll.Should().BeTrue();
        scope.ToTheaterFilter().Should().BeNull();
        scope.Resolve(_theaterB).Should().Be(_theaterB);
        var act = () => scope.Resolve(null);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void StaffScope_RegionalManager_CarriesSeveralTheaters()
    {
        Principal(RoleNames.RegionalManager, _theaterA, _theaterB).TryGetStaffScope(out var scope);

        scope.IsAll.Should().BeFalse();
        scope.TheaterIds.Should().BeEquivalentTo(new[] { _theaterA, _theaterB });
        scope.Resolve(_theaterB).Should().Be(_theaterB);
        var act = () => scope.Resolve(null);
        act.Should().Throw<InvalidOperationException>();
        var outside = () => scope.Resolve(Guid.NewGuid());
        outside.Should().Throw<AccessDeniedException>();
    }

    [Fact]
    public void StaffScope_RegionalManagerWithoutTheaters_CanResolveNothing()
    {
        Principal(RoleNames.RegionalManager).TryGetStaffScope(out var scope).Should().BeTrue();

        var act = () => scope.Resolve(_theaterA);

        act.Should().Throw<AccessDeniedException>();
    }

    // ── Exception mapping: authorization failures are 403, never 401 ─────────

    private sealed class Probe : ApiControllerBase
    {
        public IActionResult Map(Exception e)
        {
            return HandleException(e, nameof(Map));
        }
    }

    [Fact]
    public void HandleException_AccessDenied_Maps403_NotUnauthorized()
    {
        var result = new Probe().Map(new AccessDeniedException("nope"));

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public void HandleException_MissingIdentityClaim_StaysUnauthorized401()
    {
        var result = new Probe().Map(new UnauthorizedAccessException("User ID claim not found."));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public void OutOfScopeStaffRequest_EndsAs403_NotAs401()
    {
        Principal(RoleNames.BoxOfficeStaff, _theaterA).TryGetStaffScope(out var scope);

        IActionResult result;
        try
        {
            scope.Resolve(_theaterB);
            result = new OkResult();
        }
        catch (Exception e)
        {
            result = new Probe().Map(e);
        }

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }
}

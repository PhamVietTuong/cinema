using System.Security.Claims;
using Cinema.Business.Contracts;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.Gate;
using Cinema.Data.Enums;
using Cinema.Service.WebApiHost.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cinema.Service.WebApiHost.Tests;

public class GateControllerTests
{
    private readonly Guid _theaterA = Guid.NewGuid();
    private readonly Guid _theaterB = Guid.NewGuid();

    private sealed class FakeGate : IGateManager
    {
        public Guid? ScannedTheater { get; private set; }

        public Task<ScanTicketResultDTO> ScanAsync(Guid theaterId, Guid userId, ScanTicketRequest request)
        {
            ScannedTheater = theaterId;
            return Task.FromResult(new ScanTicketResultDTO { Outcome = ScanOutcome.AlreadyUsed });
        }

        public Task<List<GateLookupResultDTO>> LookupAsync(Guid theaterId, GateLookupRequest request)
        {
            return Task.FromResult(new List<GateLookupResultDTO>());
        }
    }

    private static CinemaController Controller(FakeGate gate, string role, params Guid[] theaterIds)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role)
        };
        claims.AddRange(theaterIds.Select(id => new Claim("theaterId", id.ToString())));
        return CinemaControllerFactory.Create(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), gate: gate);
    }

    [Fact]
    public async Task Scan_Customer_IsForbidden403_NotUnauthorized()
    {
        var result = await Controller(new FakeGate(), RoleNames.Customer, _theaterA)
            .Scan(new ScanTicketRequest { Code = "x" });

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Lookup_Customer_IsForbidden()
    {
        var result = await Controller(new FakeGate(), RoleNames.Customer, _theaterA)
            .Lookup(new GateLookupRequest { InvoiceCode = "x" });

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Scan_TheaterStaffWithoutTheaterClaim_IsForbidden()
    {
        var result = await Controller(new FakeGate(), RoleNames.TheaterStaff)
            .Scan(new ScanTicketRequest { Code = "x" });

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Scan_OtherTheaterThanScope_Is403()
    {
        var gate = new FakeGate();

        var result = await Controller(gate, RoleNames.TheaterStaff, _theaterA)
            .Scan(new ScanTicketRequest { Code = "x", TheaterId = _theaterB });

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        gate.ScannedTheater.Should().BeNull();
    }

    [Fact]
    public async Task Scan_RefusedScan_IsHttp200WithOutcome_AndDefaultsToOwnTheater()
    {
        var gate = new FakeGate();

        var result = await Controller(gate, RoleNames.TheaterStaff, _theaterA)
            .Scan(new ScanTicketRequest { Code = "x" });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ScanTicketResultDTO>().Which.Outcome.Should().Be(ScanOutcome.AlreadyUsed);
        gate.ScannedTheater.Should().Be(_theaterA);
    }
}

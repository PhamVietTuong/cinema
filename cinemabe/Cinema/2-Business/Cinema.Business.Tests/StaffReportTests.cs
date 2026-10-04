using System.IdentityModel.Tokens.Jwt;
using Cinema.Business.Contracts.Exceptions;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.Staff;
using Cinema.Business.Managers;
using Cinema.Business.Security;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using Cinema.Data.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Cinema.Business.Tests;

public class StaffReportTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly Guid _theaterA = Guid.NewGuid();
    private readonly Guid _theaterB = Guid.NewGuid();
    private SalesQuery? _lastQuery;

    private StaffReportManager Sut()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Business:TimeZoneId"] = "UTC",
            ["Business:DayCutoffHour"] = "6"
        }).Build();
        return new StaffReportManager(_uowMock.Object, config, TimeProvider.System);
    }

    private static StaffReportRequest Request(SalesGroupBy groupBy, params Guid[] theaters)
    {
        return new StaffReportRequest
        {
            From = new DateTime(2026, 10, 1),
            To = new DateTime(2026, 10, 3),
            GroupBy = groupBy,
            TheaterIds = theaters.ToList()
        };
    }

    private void SetupSales(SalesAggregates aggregates)
    {
        _uowMock.Setup(u => u.StaffReportStore.GetSalesAsync(It.IsAny<SalesQuery>()))
            .Callback<SalesQuery>(q => _lastQuery = q)
            .ReturnsAsync(aggregates);
    }

    [Fact]
    public async Task Sales_ByDay_IsNetOfRefunds_SplitsTicketAndFood_AndFillsEmptyDays()
    {
        SetupSales(new SalesAggregates
        {
            Sold = new List<SalesAggregateRow>
            {
                new() { DayKey = new DateTime(2026, 10, 1), InvoiceCount = 2, TicketAmount = 300, FoodAmount = 100, FinalAmount = 380, DiscountAmount = 20 },
                new() { DayKey = new DateTime(2026, 10, 3), InvoiceCount = 1, TicketAmount = 100, FoodAmount = 0, FinalAmount = 100 }
            },
            Refunded = new List<SalesAggregateRow>
            {
                new() { DayKey = new DateTime(2026, 10, 1), InvoiceCount = 1, TicketAmount = 100, FoodAmount = 20, FinalAmount = 110, DiscountAmount = 10 }
            }
        });

        var report = await Sut().GetSalesAsync(Request(SalesGroupBy.Day), null);

        report.Rows.Select(r => r.Key).Should().Equal("2026-10-01", "2026-10-02", "2026-10-03");
        var first = report.Rows[0];
        first.TicketRevenue.Should().Be(200);
        first.FoodRevenue.Should().Be(80);
        first.DiscountAmount.Should().Be(10);
        first.NetRevenue.Should().Be(270);
        first.RefundCount.Should().Be(1);
        first.RefundAmount.Should().Be(110);
        report.Rows[1].NetRevenue.Should().Be(0);
        report.Totals.NetRevenue.Should().Be(370);
        report.Totals.TicketRevenue.Should().Be(300);
        report.Totals.InvoiceCount.Should().Be(3);
        // Business day window: 06:00 on the 1st through 06:00 on the 4th (UTC zone in this test).
        _lastQuery!.FromUtc.Should().Be(new DateTime(2026, 10, 1, 6, 0, 0));
        _lastQuery.ToUtc.Should().Be(new DateTime(2026, 10, 4, 6, 0, 0));
        _lastQuery.DayShiftMinutes.Should().Be(-360);
    }

    [Fact]
    public async Task Sales_ByMovie_LeavesFoodUnattributed_AndResolvesTitles()
    {
        var movieId = Guid.NewGuid();
        SetupSales(new SalesAggregates
        {
            Sold = new List<SalesAggregateRow> { new() { GuidKey = movieId, InvoiceCount = 4, TicketAmount = 400, FinalAmount = 400 } }
        });
        _uowMock.Setup(u => u.StaffReportStore.GetMovieTitlesAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [movieId] = "Dune" });

        var report = await Sut().GetSalesAsync(Request(SalesGroupBy.Movie), null);

        report.Rows.Should().ContainSingle();
        report.Rows[0].Label.Should().Be("Dune");
        report.Rows[0].TicketRevenue.Should().Be(400);
        report.Rows[0].FoodRevenue.Should().BeNull();
        report.Rows[0].NetRevenue.Should().Be(400);
    }

    [Fact]
    public async Task Sales_ByPaymentMethod_UsesTenderNames()
    {
        SetupSales(new SalesAggregates
        {
            Sold = new List<SalesAggregateRow>
            {
                new() { IntKey = (int)PaymentTender.Cash, InvoiceCount = 2, FinalAmount = 150 },
                new() { IntKey = (int)PaymentTender.Card, InvoiceCount = 1, FinalAmount = 250 }
            }
        });

        var report = await Sut().GetSalesAsync(Request(SalesGroupBy.PaymentMethod), null);

        report.Rows.Select(r => r.Key).Should().Equal("Card", "Cash");
        report.Totals.NetRevenue.Should().Be(400);
        report.Totals.TicketRevenue.Should().BeNull();
    }

    [Fact]
    public async Task Sales_TheaterManagerRequestingAnotherTheater_Throws403Exception()
    {
        var act = () => Sut().GetSalesAsync(Request(SalesGroupBy.Day, _theaterB), new[] { _theaterA });

        await act.Should().ThrowAsync<AccessDeniedException>();
        _uowMock.Verify(u => u.StaffReportStore.GetSalesAsync(It.IsAny<SalesQuery>()), Times.Never);
    }

    [Fact]
    public async Task Sales_RegionalManager_WithoutRequestedTheaters_IsLimitedToAssignedTheaters()
    {
        SetupSales(new SalesAggregates());

        await Sut().GetSalesAsync(Request(SalesGroupBy.Theater), new[] { _theaterA, _theaterB });

        _lastQuery!.TheaterIds.Should().BeEquivalentTo(new[] { _theaterA, _theaterB });
    }

    [Fact]
    public async Task Sales_RegionalManagerWithNoAssignments_GetsEmptyReport_WithoutQuerying()
    {
        var report = await Sut().GetSalesAsync(Request(SalesGroupBy.Day), Array.Empty<Guid>());

        report.Rows.Should().BeEmpty();
        _uowMock.Verify(u => u.StaffReportStore.GetSalesAsync(It.IsAny<SalesQuery>()), Times.Never);
    }

    [Fact]
    public async Task Sales_RangeOver92Days_IsRejected()
    {
        var request = Request(SalesGroupBy.Day);
        request.To = request.From.AddDays(92);

        var act = () => Sut().GetSalesAsync(request, null);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Sales_IssuesAFixedNumberOfStoreCalls_RegardlessOfRowCount()
    {
        var rows = Enumerable.Range(0, 500).Select(i => new SalesAggregateRow { GuidKey = Guid.NewGuid(), InvoiceCount = 1, FinalAmount = i }).ToList();
        SetupSales(new SalesAggregates { Sold = rows });
        _uowMock.Setup(u => u.UserStore.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>())).ReturnsAsync(new Dictionary<Guid, string>());

        await Sut().GetSalesAsync(Request(SalesGroupBy.Staff), null);

        _uowMock.Verify(u => u.StaffReportStore.GetSalesAsync(It.IsAny<SalesQuery>()), Times.Once);
        _uowMock.Verify(u => u.UserStore.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()), Times.Once);
    }

    [Fact]
    public async Task Occupancy_ComputesPerScreeningAndTotalRates()
    {
        _uowMock.Setup(u => u.StaffReportStore.GetOccupancyAsync(It.IsAny<OccupancyQuery>())).ReturnsAsync(new List<OccupancyRow>
        {
            new() { ShowTimeId = Guid.NewGuid(), SoldSeats = 30, ActiveSeats = 60 },
            new() { ShowTimeId = Guid.NewGuid(), SoldSeats = 0, ActiveSeats = 0 }
        });

        var report = await Sut().GetOccupancyAsync(Request(SalesGroupBy.Day), null);

        report.Rows[0].OccupancyRate.Should().Be(0.5);
        report.Rows[1].OccupancyRate.Should().Be(0);
        report.TotalSoldSeats.Should().Be(30);
        report.OccupancyRate.Should().Be(0.5);
    }

    [Fact]
    public async Task Kpis_ComputeAttachRefundRatesAndSpendPerHead()
    {
        _uowMock.Setup(u => u.StaffReportStore.GetKpiRawAsync(It.IsAny<SalesQuery>())).ReturnsAsync(new KpiRaw
        {
            SoldInvoices = 10,
            SoldAmount = 1000,
            RefundedInvoices = 1,
            RefundedAmount = 80,
            TicketInvoices = 8,
            TicketInvoicesAmount = 900,
            TicketInvoicesWithFood = 4,
            Tickets = 18
        });

        var kpis = await Sut().GetKpisAsync(Request(SalesGroupBy.Day), null);

        kpis.AttachRate.Should().Be(0.5);
        kpis.RefundRateByCount.Should().BeApproximately(0.1, 1e-9);
        kpis.RefundRateByAmount.Should().BeApproximately(0.08, 1e-9);
        kpis.AverageSpendPerHead.Should().Be(50);
    }

    [Fact]
    public async Task Kpis_NoData_ReturnsZeros()
    {
        _uowMock.Setup(u => u.StaffReportStore.GetKpiRawAsync(It.IsAny<SalesQuery>())).ReturnsAsync(new KpiRaw());

        var kpis = await Sut().GetKpisAsync(Request(SalesGroupBy.Day), null);

        kpis.AttachRate.Should().Be(0);
        kpis.AverageSpendPerHead.Should().Be(0);
        kpis.RefundRateByAmount.Should().Be(0);
    }

    // ── RegionalManager assignments ──────────────────────────────────────────

    private static User Regional(params Guid[] theaters)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "r@x.vn",
            Name = "Regional",
            Status = UserStatus.Active,
            UserType = new UserType { Id = Guid.NewGuid(), Name = RoleNames.RegionalManager }
        };
        foreach (var id in theaters)
        {
            user.UserTheaters.Add(new UserTheater { UserId = user.Id, TheaterId = id });
        }
        return user;
    }

    [Fact]
    public void Jwt_RegionalManager_GetsOneTheaterClaimPerAssignment_AndOtherRolesKeepSingleClaim()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JWT:Secret"] = "0123456789abcdef0123456789abcdef",
            ["JWT:Issuer"] = "i",
            ["JWT:Audience"] = "a"
        }).Build();
        var sut = new JwtTokenService(config);

        var regional = new JwtSecurityTokenHandler().ReadJwtToken(sut.GenerateToken(Regional(_theaterA, _theaterB)));
        regional.Claims.Where(c => c.Type == "theaterId").Select(c => c.Value)
            .Should().BeEquivalentTo(new[] { _theaterA.ToString(), _theaterB.ToString() });

        var manager = Regional();
        manager.UserType.Name = RoleNames.TheaterManager;
        manager.TheaterId = _theaterA;
        var single = new JwtSecurityTokenHandler().ReadJwtToken(sut.GenerateToken(manager));
        single.Claims.Where(c => c.Type == "theaterId").Should().ContainSingle();
    }

    private ManagerOverrideService Overrides(User approver, out List<AuditLog> staged)
    {
        var rows = new List<AuditLog>();
        staged = rows;
        _uowMock.Setup(u => u.AuditLogStore.Stage(It.IsAny<AuditLog>())).Callback<AuditLog>(rows.Add);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(approver.Id)).ReturnsAsync(approver);
        return new ManagerOverrideService(_uowMock.Object, new AuditLogger(_uowMock.Object));
    }

    [Fact]
    public async Task Override_RegionalManager_ApprovesOnlyAssignedTheaters()
    {
        var regional = Regional(_theaterA);
        var sut = Overrides(regional, out _);

        (await sut.VerifyAsync(_theaterA, regional.Id, null, AuditAction.Refund)).Should().Be(regional.Id);

        var act = () => sut.VerifyAsync(_theaterB, regional.Id, null, AuditAction.Refund);
        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    public async Task GetApprovers_AsksTheStoreToIncludeAssignedRegionalManagers()
    {
        _uowMock.Setup(u => u.UserStore.GetApproversAsync(
                _theaterA, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<IReadOnlyCollection<string>>()))
            .ReturnsAsync(new List<(Guid, string)> { (Guid.NewGuid(), "Regional") });
        var sut = new ManagerOverrideService(_uowMock.Object, new AuditLogger(_uowMock.Object));

        var approvers = await sut.GetApproversAsync(_theaterA);

        approvers.Should().ContainSingle();
        _uowMock.Verify(u => u.UserStore.GetApproversAsync(
            _theaterA,
            It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<IReadOnlyCollection<string>>(),
            It.Is<IReadOnlyCollection<string>>(r => r.Contains(RoleNames.RegionalManager))), Times.Once);
    }
}

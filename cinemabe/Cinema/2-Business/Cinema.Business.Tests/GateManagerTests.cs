using System.Text.Json;
using Cinema.Business.DTO.Gate;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Cinema.Business.Tests;

public class GateManagerTests
{
    private const string _qr = "QR-1";

    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedClock(DateTime utcNow)
        {
            _now = new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
        }

        public override DateTimeOffset GetUtcNow() => _now;

        // Local time == UTC, so the test's "now" is also the theater's local now.
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly Guid _theaterId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 10, 4, 18, 0, 0);
    private readonly List<AuditLog> _logs = new();

    private GateTicketRow _ticket;

    public GateManagerTests()
    {
        _ticket = new GateTicketRow
        {
            InvoiceId = Guid.NewGuid(),
            ShowTimeId = Guid.NewGuid(),
            SeatId = Guid.NewGuid(),
            IsActive = true,
            InvoiceStatus = InvoiceStatus.Paid,
            InvoiceCode = "CIN1",
            SeatLabel = "A1",
            MovieTitle = "Movie",
            RoomName = "R1",
            TheaterId = _theaterId,
            StartTime = _now.AddMinutes(10),
            EndTime = _now.AddMinutes(130),
            AgeRatingCode = "P",
            MinAge = 0
        };
        _uowMock.Setup(u => u.InvoiceStore.GetGateTicketByQrAsync(_qr)).ReturnsAsync(() => _ticket);
        _uowMock.Setup(u => u.InvoiceStore.TryAdmitTicketAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(true);
        _uowMock.Setup(u => u.AuditLogStore.Stage(It.IsAny<AuditLog>())).Callback<AuditLog>(row => _logs.Add(row));
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
    }

    private GateManager Sut(Dictionary<string, string?>? settings = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings ?? new Dictionary<string, string?>()).Build();
        return new GateManager(_uowMock.Object, new AuditLogger(_uowMock.Object), config, new FixedClock(_now));
    }

    private Task<ScanTicketResultDTO> Scan(ScanTicketRequest? request = null, GateManager? sut = null)
    {
        return (sut ?? Sut()).ScanAsync(_theaterId, _userId, request ?? new ScanTicketRequest { Code = _qr });
    }

    private void VerifyNotAdmitted()
    {
        _uowMock.Verify(u => u.InvoiceStore.TryAdmitTicketAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task Scan_ValidTicket_IsAdmitted_WithDetails()
    {
        var result = await Scan();

        result.Outcome.Should().Be(ScanOutcome.Admitted);
        result.InvoiceCode.Should().Be("CIN1");
        result.SeatLabel.Should().Be("A1");
        result.MovieTitle.Should().Be("Movie");
        _uowMock.Verify(u => u.InvoiceStore.TryAdmitTicketAsync(
            _ticket.InvoiceId, _ticket.SeatId, _ticket.ShowTimeId, _userId, _now), Times.Once);
    }

    [Fact]
    public async Task Scan_UnknownCode_IsNotFound()
    {
        var result = await Scan(new ScanTicketRequest { Code = "nope" });

        result.Outcome.Should().Be(ScanOutcome.NotFound);
        VerifyNotAdmitted();
    }

    [Fact]
    public async Task Scan_BlankCode_IsNotFound()
    {
        (await Scan(new ScanTicketRequest { Code = "  " })).Outcome.Should().Be(ScanOutcome.NotFound);
    }

    [Theory]
    [InlineData(InvoiceStatus.Pending)]
    [InlineData(InvoiceStatus.Cancelled)]
    [InlineData(InvoiceStatus.Refunded)]
    public async Task Scan_InvoiceNotPaid_IsNotPaid(InvoiceStatus status)
    {
        _ticket.InvoiceStatus = status;

        (await Scan()).Outcome.Should().Be(ScanOutcome.NotPaid);
        VerifyNotAdmitted();
    }

    [Fact]
    public async Task Scan_InactiveTicket_IsNotPaid()
    {
        _ticket.IsActive = false;

        (await Scan()).Outcome.Should().Be(ScanOutcome.NotPaid);
    }

    [Fact]
    public async Task Scan_UsedTicket_IsAlreadyUsed_WithUsedAtAndBy()
    {
        var usedAt = _now.AddMinutes(-5);
        _ticket.IsUsed = true;
        _ticket.UsedAt = usedAt;
        _ticket.UsedByName = "Gate Guy";

        var result = await Scan();

        result.Outcome.Should().Be(ScanOutcome.AlreadyUsed);
        result.UsedAt.Should().Be(usedAt);
        result.UsedBy.Should().Be("Gate Guy");
        VerifyNotAdmitted();
    }

    [Fact]
    public async Task Scan_TicketOfAnotherTheater_IsWrongTheater_AndRevealsNothing()
    {
        _ticket.TheaterId = Guid.NewGuid();

        var result = await Scan();

        result.Outcome.Should().Be(ScanOutcome.WrongTheater);
        result.MovieTitle.Should().BeEmpty();
        VerifyNotAdmitted();
    }

    [Fact]
    public async Task Scan_UsedTicketOfAnotherTheater_IsWrongTheater_AndDoesNotLeakWhoUsedIt()
    {
        _ticket.TheaterId = Guid.NewGuid();
        _ticket.IsUsed = true;
        _ticket.UsedAt = _now.AddMinutes(-5);
        _ticket.UsedByName = "Gate Guy";

        var result = await Scan();

        result.Outcome.Should().Be(ScanOutcome.WrongTheater);
        result.UsedBy.Should().BeNull();
        result.UsedAt.Should().BeNull();
        result.MovieTitle.Should().BeEmpty();
        VerifyNotAdmitted();
    }

    [Fact]
    public async Task Scan_ShowTimeFilterMismatch_IsWrongShowTime()
    {
        var result = await Scan(new ScanTicketRequest { Code = _qr, ShowTimeId = Guid.NewGuid() });

        result.Outcome.Should().Be(ScanOutcome.WrongShowTime);
        VerifyNotAdmitted();
    }

    [Fact]
    public async Task Scan_ShowTimeFilterMatch_IsAdmitted()
    {
        var result = await Scan(new ScanTicketRequest { Code = _qr, ShowTimeId = _ticket.ShowTimeId });

        result.Outcome.Should().Be(ScanOutcome.Admitted);
    }

    [Fact]
    public async Task Scan_MoreThanThirtyMinutesBeforeStart_IsTooEarly()
    {
        _ticket.StartTime = _now.AddMinutes(31);
        _ticket.EndTime = _now.AddMinutes(150);

        (await Scan()).Outcome.Should().Be(ScanOutcome.TooEarly);
        VerifyNotAdmitted();
    }

    [Fact]
    public async Task Scan_ExactlyThirtyMinutesBeforeStart_IsAdmitted()
    {
        _ticket.StartTime = _now.AddMinutes(30);
        _ticket.EndTime = _now.AddMinutes(150);

        (await Scan()).Outcome.Should().Be(ScanOutcome.Admitted);
    }

    [Fact]
    public async Task Scan_AdmitWindowIsConfigurable()
    {
        _ticket.StartTime = _now.AddMinutes(45);
        _ticket.EndTime = _now.AddMinutes(150);
        var sut = Sut(new Dictionary<string, string?> { ["Gate:AdmitBeforeMinutes"] = "60" });

        (await Scan(sut: sut)).Outcome.Should().Be(ScanOutcome.Admitted);
    }

    [Fact]
    public async Task Scan_AfterEndTime_IsExpired()
    {
        _ticket.StartTime = _now.AddMinutes(-130);
        _ticket.EndTime = _now.AddMinutes(-1);

        (await Scan()).Outcome.Should().Be(ScanOutcome.Expired);
        VerifyNotAdmitted();
    }

    [Fact]
    public async Task Scan_LateButBeforeEnd_IsAdmitted()
    {
        _ticket.StartTime = _now.AddMinutes(-60);
        _ticket.EndTime = _now.AddMinutes(60);

        (await Scan()).Outcome.Should().Be(ScanOutcome.Admitted);
    }

    [Fact]
    public async Task Scan_T18Ticket_NeedsAgeCheck_AndIsNotMarkedUsed()
    {
        _ticket.AgeRatingCode = "T18";
        _ticket.MinAge = 18;

        var result = await Scan();

        result.Outcome.Should().Be(ScanOutcome.AgeCheckRequired);
        result.MinAge.Should().Be(18);
        result.AgeRatingCode.Should().Be("T18");
        _ticket.IsUsed.Should().BeFalse();
        VerifyNotAdmitted();
    }

    [Fact]
    public async Task Scan_T16Ticket_AgeConfirmed_IsAdmitted()
    {
        _ticket.MinAge = 16;

        var result = await Scan(new ScanTicketRequest { Code = _qr, AgeConfirmed = true });

        result.Outcome.Should().Be(ScanOutcome.Admitted);
    }

    [Fact]
    public async Task Scan_T13Ticket_DoesNotPromptForAge()
    {
        _ticket.MinAge = 13;

        (await Scan()).Outcome.Should().Be(ScanOutcome.Admitted);
    }

    [Fact]
    public async Task Scan_ConcurrentDoubleScan_AdmitsExactlyOnce()
    {
        // The atomic UPDATE changes the row for exactly one caller, whatever the interleaving.
        var winners = 0;
        _uowMock.Setup(u => u.InvoiceStore.TryAdmitTicketAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(() => Interlocked.Increment(ref winners) == 1);

        var results = await Task.WhenAll(Scan(), Scan());

        results.Count(r => r.Outcome == ScanOutcome.Admitted).Should().Be(1);
        results.Count(r => r.Outcome == ScanOutcome.AlreadyUsed).Should().Be(1);
    }

    private AuditLog SingleLog()
    {
        return _logs.Should().ContainSingle().Subject;
    }

    private static JsonElement LogData(AuditLog log)
    {
        return JsonDocument.Parse(log.DataJson!).RootElement;
    }

    private static int LoggedOutcome(AuditLog log)
    {
        return LogData(log).GetProperty(nameof(ScanTicketResultDTO.Outcome)).GetInt32();
    }

    [Fact]
    public async Task Log_UnknownCode_StagesNotFound_WithNoIdsAndNoSnapshot()
    {
        await Scan(new ScanTicketRequest { Code = "nope" });

        var log = SingleLog();
        log.Action.Should().Be(AuditAction.GateScan);
        log.TheaterId.Should().Be(_theaterId);
        log.ActorUserId.Should().Be(_userId);
        log.EntityId.Should().BeNull();
        LoggedOutcome(log).Should().Be((int)ScanOutcome.NotFound);
        var data = LogData(log);
        data.GetProperty("InvoiceId").ValueKind.Should().Be(JsonValueKind.Null);
        data.GetProperty("Snapshot").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData(InvoiceStatus.Pending)]
    public async Task Log_NotPaid_StagesOutcome_WithIdsButNoSnapshot(InvoiceStatus status)
    {
        _ticket.InvoiceStatus = status;

        await Scan();

        var log = SingleLog();
        LoggedOutcome(log).Should().Be((int)ScanOutcome.NotPaid);
        log.EntityId.Should().Be(_ticket.InvoiceId);
        LogData(log).GetProperty("Snapshot").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Log_WrongTheater_StagesOutcome_WithIdsButNoSnapshot()
    {
        _ticket.TheaterId = Guid.NewGuid();

        await Scan();

        var log = SingleLog();
        LoggedOutcome(log).Should().Be((int)ScanOutcome.WrongTheater);
        log.EntityId.Should().Be(_ticket.InvoiceId);
        LogData(log).GetProperty("Snapshot").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Log_AlreadyUsed_StagesOutcome_WithSnapshot()
    {
        _ticket.IsUsed = true;

        await Scan();

        var log = SingleLog();
        LoggedOutcome(log).Should().Be((int)ScanOutcome.AlreadyUsed);
        log.EntityId.Should().Be(_ticket.InvoiceId);
        LogData(log).GetProperty("Snapshot").GetProperty("InvoiceCode").GetString().Should().Be("CIN1");
    }

    [Fact]
    public async Task Log_WrongShowTime_StagesOutcome_WithSnapshot()
    {
        await Scan(new ScanTicketRequest { Code = _qr, ShowTimeId = Guid.NewGuid() });

        var log = SingleLog();
        LoggedOutcome(log).Should().Be((int)ScanOutcome.WrongShowTime);
        LogData(log).GetProperty("Snapshot").GetProperty("SeatLabel").GetString().Should().Be("A1");
    }

    [Fact]
    public async Task Log_TooEarly_StagesOutcome_WithSnapshot()
    {
        _ticket.StartTime = _now.AddMinutes(31);
        _ticket.EndTime = _now.AddMinutes(150);

        await Scan();

        var log = SingleLog();
        LoggedOutcome(log).Should().Be((int)ScanOutcome.TooEarly);
        LogData(log).GetProperty("Snapshot").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Log_Expired_StagesOutcome_WithSnapshot()
    {
        _ticket.StartTime = _now.AddMinutes(-130);
        _ticket.EndTime = _now.AddMinutes(-1);

        await Scan();

        var log = SingleLog();
        LoggedOutcome(log).Should().Be((int)ScanOutcome.Expired);
        LogData(log).GetProperty("Snapshot").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Log_AgeCheckRequired_StagesOutcome_WithSnapshot()
    {
        _ticket.AgeRatingCode = "T18";
        _ticket.MinAge = 18;

        await Scan();

        var log = SingleLog();
        LoggedOutcome(log).Should().Be((int)ScanOutcome.AgeCheckRequired);
        LogData(log).GetProperty("Snapshot").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Log_Admitted_StagesOutcome_WithSnapshotAndRequestedShowTimeId()
    {
        var requestedShowTimeId = _ticket.ShowTimeId;

        await Scan(new ScanTicketRequest { Code = _qr, ShowTimeId = requestedShowTimeId });

        var log = SingleLog();
        LoggedOutcome(log).Should().Be((int)ScanOutcome.Admitted);
        log.EntityId.Should().Be(_ticket.InvoiceId);
        var data = LogData(log);
        data.GetProperty("RequestedShowTimeId").GetGuid().Should().Be(requestedShowTimeId);
        data.GetProperty("Snapshot").GetProperty("MovieTitle").GetString().Should().Be("Movie");
    }

    [Fact]
    public async Task Log_ConcurrentDoubleScan_LogsExactlyOneAdmittedAndOneAlreadyUsed()
    {
        var winners = 0;
        _uowMock.Setup(u => u.InvoiceStore.TryAdmitTicketAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(() => Interlocked.Increment(ref winners) == 1);

        await Task.WhenAll(Scan(), Scan());

        _logs.Should().HaveCount(2);
        _logs.Count(l => LoggedOutcome(l) == (int)ScanOutcome.Admitted).Should().Be(1);
        _logs.Count(l => LoggedOutcome(l) == (int)ScanOutcome.AlreadyUsed).Should().Be(1);
    }

    [Fact]
    public async Task Log_SaveChangesThrows_ScanStillReturnsCorrectResult_AndDoesNotThrow()
    {
        _uowMock.Setup(u => u.SaveChangesAsync()).ThrowsAsync(new InvalidOperationException("db down"));

        var result = await Scan();

        result.Outcome.Should().Be(ScanOutcome.Admitted);
    }

    [Fact]
    public async Task Log_OverlongCode_IsTruncatedTo200Chars()
    {
        var longCode = new string('x', 250);

        await Scan(new ScanTicketRequest { Code = longCode });

        var log = SingleLog();
        LogData(log).GetProperty("Code").GetString().Should().HaveLength(200);
    }

    [Fact]
    public async Task Lookup_WithoutCodeOrPhone_IsRejected()
    {
        var act = () => Sut().LookupAsync(_theaterId, new GateLookupRequest());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Lookup_GroupsTicketsPerInvoice_AndMasksPhone()
    {
        var start = _now.AddMinutes(20);
        _uowMock.Setup(u => u.InvoiceStore.FindTicketsForLookupAsync(
                _theaterId, "CIN1", null, _now.Date, _now.Date.AddDays(1)))
            .ReturnsAsync(new List<GateLookupRow>
            {
                new() { InvoiceCode = "CIN1", CustomerName = "An", CustomerPhone = "0901234567", QrCode = "q1", SeatLabel = "A1", StartTime = start },
                new() { InvoiceCode = "CIN1", CustomerName = "An", CustomerPhone = "0901234567", QrCode = "q2", SeatLabel = "A2", StartTime = start, IsUsed = true }
            });

        var result = await Sut().LookupAsync(_theaterId, new GateLookupRequest { InvoiceCode = " CIN1 " });

        var invoice = result.Should().ContainSingle().Subject;
        invoice.MaskedPhone.Should().Be("*******567");
        invoice.Tickets.Select(t => t.QrCode).Should().Equal("q1", "q2");
        invoice.Tickets[1].IsUsed.Should().BeTrue();
    }
}

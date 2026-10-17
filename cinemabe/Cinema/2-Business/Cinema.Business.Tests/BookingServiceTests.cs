using System.Linq.Expressions;
using Cinema.Business.Contracts;
using Cinema.Business.Contracts.Payments;
using Cinema.Business.DTO.Booking;
using Cinema.Business.DTO.Requests;
using Cinema.Business.Managers;
using Cinema.Business.Notifications;
using Cinema.Business.Payments;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class BookingServiceTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly Mock<ISeatNotificationService> _seatNotificationsMock = new();
    private readonly BookingManager _sut;

    private static readonly Guid ShowTimeId1 = Guid.NewGuid();
    private static readonly Guid ShowTimeId2 = Guid.NewGuid();
    private static readonly Guid RoomId1     = Guid.NewGuid();
    private static readonly Guid RoomId2     = Guid.NewGuid();
    private static readonly Guid SeatId1     = Guid.NewGuid();
    private static readonly Guid SeatId5     = Guid.NewGuid();
    private static readonly Guid SeatId10    = Guid.NewGuid();
    private static readonly Guid SeatId11    = Guid.NewGuid();
    private static readonly Guid SeatId20    = Guid.NewGuid();
    private static readonly Guid SeatId21    = Guid.NewGuid();

    public BookingServiceTests()
    {
        var gateways = new PaymentGatewayResolver(new IPaymentGateway[] { new SandboxPaymentGateway() }, "Sandbox");
        _sut = new BookingManager(_uowMock.Object, gateways, new DevLogNotificationService(), new DevLogSmsNotificationService(), _seatNotificationsMock.Object);

        // Default: no RoomType price overrides for any category. Tests that care about an override
        // set this up explicitly.
        _uowMock.Setup(u => u.RoomTypePatronCategoryPriceStore.FindByPatronCategoriesAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<RoomTypePatronCategoryPrice>());
        // Default: no seats share a SeatGroupId (no pair-integrity concerns) unless a test overrides it.
        _uowMock.Setup(u => u.SeatStore.FindAsync(It.IsAny<Expression<Func<Seat, bool>>>()))
            .ReturnsAsync(new List<Seat>());
        // Default: theater has no configured SeatType kinds unless a test sets them up.
        _uowMock.Setup(u => u.SeatTypeStore.GetKindMapAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new Dictionary<SeatKind, Guid>());
        // Default: no active patron categories in the theater unless a test sets them up.
        _uowMock.Setup(u => u.PatronCategoryStore.FindAsync(It.IsAny<Expression<Func<PatronCategory, bool>>>()))
            .ReturnsAsync(new List<PatronCategory>());
        // Default: no stock ledger rows (nothing sold) unless a test sets them up.
        _uowMock.Setup(u => u.StockMovementStore.GetNetSaleQuantitiesAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<StockNetQuantity>());
        // Default: no auto-apply PromotionProgram candidates unless a test sets them up — keeps every
        // existing booking test's pricing on today's Discount-only path.
        _uowMock.Setup(u => u.PromotionProgramStore.GetAutoApplyCandidatesAsync(It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<PromotionProgram>());
        _uowMock.Setup(u => u.HolidayStore.FindAsync(It.IsAny<Expression<Func<Holiday, bool>>>()))
            .ReturnsAsync(new List<Holiday>());
    }

    private static PagingSearchDTO SeatSearch(Guid showTimeId, Guid roomId)
    {
        return new()
        {
            Filters = new Dictionary<string, string>
            {
                ["showTimeId"] = showTimeId.ToString(),
                ["roomId"]     = roomId.ToString()
            }
        };
    }

    /// <summary>Sets up both the by-id lookup ComposeInvoiceAsync uses to validate requested seats and the
    /// whole-room lookup it uses to freeze each ticket's SeatLabel — the same seats, so both agree.</summary>
    private void SetupSeatsById(Dictionary<Guid, Seat> seats)
    {
        _uowMock.Setup(u => u.SeatStore.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>())).ReturnsAsync(seats);
        _uowMock.Setup(u => u.SeatStore.GetSeatMapByRoomNoTrackingAsync(It.IsAny<Guid>())).ReturnsAsync(seats.Values.ToList());
    }

    [Fact]
    public async Task GetSeatsAsync_ReturnsAvailableStatus_WhenNotBookedOrLocked()
    {
        var seats = new List<Seat> { new() { Id = SeatId1, RowName = "A", ColIndex = 1 } };
        _uowMock.Setup(u => u.SeatStore.GetSeatMapByRoomNoTrackingAsync(RoomId1)).ReturnsAsync(seats);
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatIdsAsync(ShowTimeId1, RoomId1)).ReturnsAsync(new List<Guid>());
        _uowMock.Setup(u => u.ShowTimeStore.GetShowTimeRoomAsync(ShowTimeId1, RoomId1)).ReturnsAsync((ShowTimeRoom?)null);

        var result = await _sut.GetSeatsAsync(SeatSearch(ShowTimeId1, RoomId1));

        result.Results.Should().HaveCount(1);
        result.Results.First().Status.Should().Be(SeatStatus.Available);
        result.Results.First().IsDouble.Should().BeFalse();
    }

    [Fact]
    public async Task GetSeatsAsync_ReturnsOccupied_WhenSeatIsBooked()
    {
        var seats = new List<Seat> { new() { Id = SeatId5, RowName = "B", ColIndex = 2 } };
        _uowMock.Setup(u => u.SeatStore.GetSeatMapByRoomNoTrackingAsync(RoomId2)).ReturnsAsync(seats);
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatIdsAsync(ShowTimeId1, RoomId2)).ReturnsAsync(new List<Guid> { SeatId5 });
        _uowMock.Setup(u => u.ShowTimeStore.GetShowTimeRoomAsync(ShowTimeId1, RoomId2)).ReturnsAsync((ShowTimeRoom?)null);

        var result = await _sut.GetSeatsAsync(SeatSearch(ShowTimeId1, RoomId2));

        result.Results.First().Status.Should().Be(SeatStatus.Occupied);
    }

    [Fact]
    public async Task GetSeatsAsync_IsDouble_DerivedFromSeatGroupId()
    {
        var groupId = Guid.NewGuid();
        var seats = new List<Seat> { new() { Id = SeatId1, RowName = "A", ColIndex = 1, SeatGroupId = groupId } };
        _uowMock.Setup(u => u.SeatStore.GetSeatMapByRoomNoTrackingAsync(RoomId1)).ReturnsAsync(seats);
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatIdsAsync(ShowTimeId1, RoomId1)).ReturnsAsync(new List<Guid>());
        _uowMock.Setup(u => u.ShowTimeStore.GetShowTimeRoomAsync(ShowTimeId1, RoomId1)).ReturnsAsync((ShowTimeRoom?)null);

        var result = await _sut.GetSeatsAsync(SeatSearch(ShowTimeId1, RoomId1));

        result.Results.First().IsDouble.Should().BeTrue();
    }

    [Fact]
    public async Task GetSeatsAsync_BlockedSeatIsLeftOut_ButStillCountsTowardDisplayNumbers()
    {
        var blockedId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var seats = new List<Seat>
        {
            new() { Id = blockedId, RowName = "C", ColIndex = 1, IsActive = false },
            new() { Id = SeatId1, RowName = "C", ColIndex = 2, SeatGroupId = groupId, IsActive = true },
            new() { Id = SeatId5, RowName = "C", ColIndex = 3, SeatGroupId = groupId, IsActive = true },
            new() { Id = SeatId10, RowName = "C", ColIndex = 4, IsActive = true },
        };
        _uowMock.Setup(u => u.SeatStore.GetSeatMapByRoomNoTrackingAsync(RoomId1)).ReturnsAsync(seats);
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatIdsAsync(ShowTimeId1, RoomId1)).ReturnsAsync(new List<Guid>());
        _uowMock.Setup(u => u.ShowTimeStore.GetShowTimeRoomAsync(ShowTimeId1, RoomId1)).ReturnsAsync((ShowTimeRoom?)null);

        var result = await _sut.GetSeatsAsync(SeatSearch(ShowTimeId1, RoomId1));

        result.Results.Should().HaveCount(3);
        result.Results.Should().NotContain(s => s.Id == blockedId);
        result.Results.Single(s => s.Id == SeatId1).DisplayNumber.Should().Be(2);
        result.Results.Single(s => s.Id == SeatId5).DisplayNumber.Should().Be(2);
        result.Results.Single(s => s.Id == SeatId10).DisplayNumber.Should().Be(3);
    }

    // ── Full pricing formula (via GetShowTimePricesAsync) ───────────────────────

    /// <summary>RoomTypePatronCategoryPrice is now the RoomType's allow-list: a category with no row
    /// is not offered there at all. This sets up "every given category is included, at its own
    /// theater-wide Price" so tests that aren't specifically exercising the allow-list can still book
    /// any category, matching the old "unrestricted" default behavior.</summary>
    private void AllowAllCategories(Guid roomTypeId, IEnumerable<PatronCategory> categories)
    {
        _uowMock.Setup(u => u.RoomTypePatronCategoryPriceStore.FindByPatronCategoriesAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(categories.Select(c => new RoomTypePatronCategoryPrice
            {
                RoomTypeId       = roomTypeId,
                PatronCategoryId = c.Id,
                Price            = c.Price,
            }).ToList());
    }

    private void SetupPricingContext(
        Guid theaterId, Guid roomTypeId, Guid showTimeId, Guid roomId, int basePrice,
        DateTime startTime, ProjectionForm projectionForm,
        IReadOnlyList<Holiday>? holidays = null,
        IReadOnlyList<TimeSlot>? timeSlots = null,
        IReadOnlyList<TicketPrice>? ticketPrices = null,
        RoomType? roomType = null,
        IReadOnlyList<PatronCategory>? categories = null,
        IReadOnlyDictionary<SeatKind, Guid>? kindMap = null)
    {
        _uowMock.Setup(u => u.ShowTimeStore.GetShowTimeRoomAsync(showTimeId, roomId))
            .ReturnsAsync(new ShowTimeRoom { ShowTimeId = showTimeId, RoomId = roomId, BasePrice = basePrice, Room = new Room { Id = roomId, TheaterId = theaterId, Status = RoomStatus.Active } });
        _uowMock.Setup(u => u.RoomStore.GetByIdAsync(roomId))
            .ReturnsAsync(new Room { Id = roomId, TheaterId = theaterId, RoomTypeId = roomTypeId });
        _uowMock.Setup(u => u.ShowTimeStore.GetByIdAsync(showTimeId))
            .ReturnsAsync(new ShowTime { Id = showTimeId, StartTime = startTime, ProjectionForm = projectionForm });
        if (roomType != null)
        {
            _uowMock.Setup(u => u.RoomTypeStore.GetByIdAsync(roomTypeId)).ReturnsAsync(roomType);
        }
        _uowMock.Setup(u => u.HolidayStore.FindAsync(It.IsAny<Expression<Func<Holiday, bool>>>()))
            .ReturnsAsync(holidays ?? new List<Holiday>());
        _uowMock.Setup(u => u.TimeSlotStore.FindAsync(It.IsAny<Expression<Func<TimeSlot, bool>>>()))
            .ReturnsAsync(timeSlots ?? new List<TimeSlot>());
        _uowMock.Setup(u => u.TicketPriceStore.FindAsync(It.IsAny<Expression<Func<TicketPrice, bool>>>()))
            .ReturnsAsync(ticketPrices ?? new List<TicketPrice>());
        _uowMock.Setup(u => u.SeatTypeStore.GetKindMapAsync(theaterId))
            .ReturnsAsync(kindMap ?? new Dictionary<SeatKind, Guid>());
        var categoryList = categories ?? new List<PatronCategory>();
        _uowMock.Setup(u => u.PatronCategoryStore.FindAsync(It.IsAny<Expression<Func<PatronCategory, bool>>>()))
            .ReturnsAsync(categoryList);
        AllowAllCategories(roomTypeId, categoryList);
    }

    [Fact]
    public async Task GetShowTimePricesAsync_FullFormula_MatchesWorkedExample()
    {
        // Deluxe/IMAX room, holiday, 19:00 slot, 3D. Adult Standard: theater-wide 90 000, RoomType
        // override 120 000. TicketPrice slot factor 1.2. ThreeDSurcharge 40 000. BasePrice surcharge
        // 30 000 (a manual per-showtime new-release premium).
        var theaterId   = Guid.NewGuid();
        var roomTypeId  = Guid.NewGuid();
        var timeSlotId  = Guid.NewGuid();
        var seatTypeId  = Guid.NewGuid();
        var adultId     = Guid.NewGuid();

        SetupPricingContext(
            theaterId, roomTypeId, ShowTimeId1, RoomId1, basePrice: 30000,
            startTime: new DateTime(2026, 3, 2, 19, 0, 0), projectionForm: ProjectionForm.ThreeD,
            roomType: new RoomType { Id = roomTypeId, ThreeDSurcharge = 40000 },
            timeSlots: new List<TimeSlot> { new() { Id = timeSlotId, TheaterId = theaterId, StartTime = "17:00", EndTime = "23:00" } },
            ticketPrices: new List<TicketPrice> { new() { TheaterId = theaterId, RoomTypeId = roomTypeId, TimeSlotId = timeSlotId, DayOfWeek = DayOfWeek.Monday, PriceMultiplier = 1.2 } },
            categories: new List<PatronCategory> { new() { Id = adultId, TheaterId = theaterId, SeatTypeId = seatTypeId, Name = "Adult", Price = 90000, IsActive = true } },
            kindMap: new Dictionary<SeatKind, Guid> { [SeatKind.Standard] = seatTypeId });
        _uowMock.Setup(u => u.RoomTypePatronCategoryPriceStore.FindByPatronCategoriesAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<RoomTypePatronCategoryPrice> { new() { RoomTypeId = roomTypeId, PatronCategoryId = adultId, Price = 120000 } });

        var result = await _sut.GetShowTimePricesAsync(ShowTimeId1, RoomId1);

        // 120000 x 1.2 + 40000 + 30000 = 214000
        result.Single().Price.Should().Be(214000);
    }

    [Fact]
    public async Task GetShowTimePricesAsync_Holiday_OverridesWeekdayMatrixRow()
    {
        var theaterId  = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var timeSlotId = Guid.NewGuid();
        var seatTypeId = Guid.NewGuid();
        var adultId    = Guid.NewGuid();

        SetupPricingContext(
            theaterId, roomTypeId, ShowTimeId1, RoomId1, basePrice: 0,
            // 2026-01-01 is a Thursday.
            startTime: new DateTime(2026, 1, 1, 19, 0, 0), projectionForm: ProjectionForm.TwoD,
            holidays: new List<Holiday> { new() { Date = new DateOnly(2026, 1, 1), PriceMultiplier = 1.5 } },
            timeSlots: new List<TimeSlot> { new() { Id = timeSlotId, TheaterId = theaterId, StartTime = "18:00", EndTime = "22:00" } },
            ticketPrices: new List<TicketPrice> { new() { TheaterId = theaterId, RoomTypeId = roomTypeId, TimeSlotId = timeSlotId, DayOfWeek = DayOfWeek.Thursday, PriceMultiplier = 2 } },
            categories: new List<PatronCategory> { new() { Id = adultId, TheaterId = theaterId, SeatTypeId = seatTypeId, Name = "Adult", Price = 100, IsActive = true } },
            kindMap: new Dictionary<SeatKind, Guid> { [SeatKind.Standard] = seatTypeId });

        var result = await _sut.GetShowTimePricesAsync(ShowTimeId1, RoomId1);

        // Holiday wins outright over the weekday matrix row: 100 x 1.5 = 150, NOT 100 x 2.
        result.Single().Price.Should().Be(150);
        _uowMock.Verify(u => u.TicketPriceStore.FindAsync(It.IsAny<Expression<Func<TicketPrice, bool>>>()), Times.Never);
    }

    [Fact]
    public async Task GetShowTimePricesAsync_HolidayFactor_AppliedWhenNoMatrixRow()
    {
        var theaterId  = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var seatTypeId = Guid.NewGuid();
        var adultId    = Guid.NewGuid();

        SetupPricingContext(
            theaterId, roomTypeId, ShowTimeId1, RoomId1, basePrice: 0,
            startTime: new DateTime(2026, 1, 1, 19, 0, 0), projectionForm: ProjectionForm.TwoD,
            holidays: new List<Holiday> { new() { Date = new DateOnly(2026, 1, 1), PriceMultiplier = 1.5 } },
            categories: new List<PatronCategory> { new() { Id = adultId, TheaterId = theaterId, SeatTypeId = seatTypeId, Name = "Adult", Price = 100, IsActive = true } },
            kindMap: new Dictionary<SeatKind, Guid> { [SeatKind.Standard] = seatTypeId });

        var result = await _sut.GetShowTimePricesAsync(ShowTimeId1, RoomId1);

        result.Single().Price.Should().Be(150);
    }

    [Fact]
    public async Task GetShowTimePricesAsync_WeekdayRow_OnlyMatchingDayApplies()
    {
        var theaterId  = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var timeSlotId = Guid.NewGuid();
        var seatTypeId = Guid.NewGuid();
        var adultId    = Guid.NewGuid();

        var rows = new List<TicketPrice>
        {
            new() { TheaterId = theaterId, RoomTypeId = roomTypeId, TimeSlotId = timeSlotId, DayOfWeek = DayOfWeek.Monday, PriceMultiplier = 1.2 },
            new() { TheaterId = theaterId, RoomTypeId = roomTypeId, TimeSlotId = timeSlotId, DayOfWeek = DayOfWeek.Friday, PriceMultiplier = 1.5 },
        };

        SetupPricingContext(
            theaterId, roomTypeId, ShowTimeId1, RoomId1, basePrice: 0,
            // 2026-03-02 is a Monday.
            startTime: new DateTime(2026, 3, 2, 19, 0, 0), projectionForm: ProjectionForm.TwoD,
            timeSlots: new List<TimeSlot> { new() { Id = timeSlotId, TheaterId = theaterId, StartTime = "18:00", EndTime = "22:00" } },
            categories: new List<PatronCategory> { new() { Id = adultId, TheaterId = theaterId, SeatTypeId = seatTypeId, Name = "Adult", Price = 100, IsActive = true } },
            kindMap: new Dictionary<SeatKind, Guid> { [SeatKind.Standard] = seatTypeId });
        _uowMock.Setup(u => u.TicketPriceStore.FindAsync(It.IsAny<Expression<Func<TicketPrice, bool>>>()))
            .ReturnsAsync((Expression<Func<TicketPrice, bool>> predicate) => rows.Where(predicate.Compile()).ToList());

        var result = await _sut.GetShowTimePricesAsync(ShowTimeId1, RoomId1);

        result.Single().Price.Should().Be(120);
    }

    [Fact]
    public async Task GetShowTimePricesAsync_NoRowForDay_FactorIsOne()
    {
        var theaterId  = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var timeSlotId = Guid.NewGuid();
        var seatTypeId = Guid.NewGuid();
        var adultId    = Guid.NewGuid();

        var rows = new List<TicketPrice>
        {
            new() { TheaterId = theaterId, RoomTypeId = roomTypeId, TimeSlotId = timeSlotId, DayOfWeek = DayOfWeek.Friday, PriceMultiplier = 1.5 },
        };

        SetupPricingContext(
            theaterId, roomTypeId, ShowTimeId1, RoomId1, basePrice: 0,
            // 2026-03-02 is a Monday; only a Friday row exists.
            startTime: new DateTime(2026, 3, 2, 19, 0, 0), projectionForm: ProjectionForm.TwoD,
            timeSlots: new List<TimeSlot> { new() { Id = timeSlotId, TheaterId = theaterId, StartTime = "18:00", EndTime = "22:00" } },
            categories: new List<PatronCategory> { new() { Id = adultId, TheaterId = theaterId, SeatTypeId = seatTypeId, Name = "Adult", Price = 100, IsActive = true } },
            kindMap: new Dictionary<SeatKind, Guid> { [SeatKind.Standard] = seatTypeId });
        _uowMock.Setup(u => u.TicketPriceStore.FindAsync(It.IsAny<Expression<Func<TicketPrice, bool>>>()))
            .ReturnsAsync((Expression<Func<TicketPrice, bool>> predicate) => rows.Where(predicate.Compile()).ToList());

        var result = await _sut.GetShowTimePricesAsync(ShowTimeId1, RoomId1);

        result.Single().Price.Should().Be(100);
    }

    [Fact]
    public async Task GetShowTimePricesAsync_RoomTypeOverride_BeatsTheaterWideDefault()
    {
        var theaterId  = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var seatTypeId = Guid.NewGuid();
        var adultId    = Guid.NewGuid();

        SetupPricingContext(
            theaterId, roomTypeId, ShowTimeId1, RoomId1, basePrice: 0,
            startTime: new DateTime(2026, 3, 2, 19, 0, 0), projectionForm: ProjectionForm.TwoD,
            categories: new List<PatronCategory> { new() { Id = adultId, TheaterId = theaterId, SeatTypeId = seatTypeId, Name = "Adult", Price = 90000, IsActive = true } },
            kindMap: new Dictionary<SeatKind, Guid> { [SeatKind.Standard] = seatTypeId });
        _uowMock.Setup(u => u.RoomTypePatronCategoryPriceStore.FindByPatronCategoriesAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<RoomTypePatronCategoryPrice> { new() { RoomTypeId = roomTypeId, PatronCategoryId = adultId, Price = 120000 } });

        var result = await _sut.GetShowTimePricesAsync(ShowTimeId1, RoomId1);

        result.Single().Price.Should().Be(120000);
    }

    [Fact]
    public async Task GetShowTimePricesAsync_RoomTypeOffersOnlyCategoriesWithARow()
    {
        var theaterId  = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var seatTypeId = Guid.NewGuid();
        var adultId    = Guid.NewGuid();
        var studentId  = Guid.NewGuid();
        var childId    = Guid.NewGuid();

        SetupPricingContext(
            theaterId, roomTypeId, ShowTimeId1, RoomId1, basePrice: 0,
            startTime: new DateTime(2026, 3, 2, 19, 0, 0), projectionForm: ProjectionForm.TwoD,
            categories: new List<PatronCategory>
            {
                new() { Id = adultId,   TheaterId = theaterId, SeatTypeId = seatTypeId, Name = "Adult",   Price = 90000, IsActive = true },
                new() { Id = studentId, TheaterId = theaterId, SeatTypeId = seatTypeId, Name = "Student", Price = 65000, IsActive = true },
                new() { Id = childId,   TheaterId = theaterId, SeatTypeId = seatTypeId, Name = "Child",   Price = 50000, IsActive = true },
            },
            kindMap: new Dictionary<SeatKind, Guid> { [SeatKind.Standard] = seatTypeId });
        // This room type's allow-list has rows for Adult and Student only — Child has none, so even
        // though Child exists theater-wide, this room type must not offer it at all.
        _uowMock.Setup(u => u.RoomTypePatronCategoryPriceStore.FindByPatronCategoriesAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<RoomTypePatronCategoryPrice>
            {
                new() { RoomTypeId = roomTypeId, PatronCategoryId = adultId,   Price = 90000 },
                new() { RoomTypeId = roomTypeId, PatronCategoryId = studentId, Price = 65000 },
            });

        var result = await _sut.GetShowTimePricesAsync(ShowTimeId1, RoomId1);

        result.Select(r => r.PatronCategoryName).Should().BeEquivalentTo(new[] { "Adult", "Student" });
    }

    [Fact]
    public async Task GetShowTimePricesAsync_RoomTypeWithNoRows_OffersNothing()
    {
        var theaterId  = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var seatTypeId = Guid.NewGuid();
        var adultId    = Guid.NewGuid();

        SetupPricingContext(
            theaterId, roomTypeId, ShowTimeId1, RoomId1, basePrice: 0,
            startTime: new DateTime(2026, 3, 2, 19, 0, 0), projectionForm: ProjectionForm.TwoD,
            categories: new List<PatronCategory> { new() { Id = adultId, TheaterId = theaterId, SeatTypeId = seatTypeId, Name = "Adult", Price = 90000, IsActive = true } },
            kindMap: new Dictionary<SeatKind, Guid> { [SeatKind.Standard] = seatTypeId });
        // Zero rows for this room type: it offers NOTHING — no fallback to the theater-wide default.
        _uowMock.Setup(u => u.RoomTypePatronCategoryPriceStore.FindByPatronCategoriesAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<RoomTypePatronCategoryPrice>());

        var result = await _sut.GetShowTimePricesAsync(ShowTimeId1, RoomId1);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateBookingAsync_RejectsCategoryNotIncludedInRoomType()
    {
        var theaterId  = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var seatTypeId = Guid.NewGuid();
        var adultId    = Guid.NewGuid();
        var childId    = Guid.NewGuid();
        var seat       = Guid.NewGuid();

        SetupBaselineBookingMocks(theaterId, roomTypeId, ShowTimeId1, RoomId1, 0);
        _uowMock.Setup(u => u.SeatTypeStore.GetKindMapAsync(theaterId))
            .ReturnsAsync(new Dictionary<SeatKind, Guid> { [SeatKind.Standard] = seatTypeId });
        _uowMock.Setup(u => u.PatronCategoryStore.FindAsync(It.IsAny<Expression<Func<PatronCategory, bool>>>()))
            .ReturnsAsync(new List<PatronCategory>
            {
                new() { Id = adultId, TheaterId = theaterId, SeatTypeId = seatTypeId, Name = "Adult", Price = 90000, IsActive = true },
                new() { Id = childId, TheaterId = theaterId, SeatTypeId = seatTypeId, Name = "Child", Price = 50000, IsActive = true },
            });
        // Only Adult is in this room type's allow-list, so Child is not offered here at all.
        _uowMock.Setup(u => u.RoomTypePatronCategoryPriceStore.FindByPatronCategoriesAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<RoomTypePatronCategoryPrice> { new() { RoomTypeId = roomTypeId, PatronCategoryId = adultId, Price = 90000 } });
        SetupSeatsById(new Dictionary<Guid, Seat> { [seat] = new() { Id = seat, RoomId = RoomId1, RowName = "H", ColIndex = 1 } });

        var request = new CreateBookingRequest
        {
            ShowTimeId    = ShowTimeId1,
            RoomId        = RoomId1,
            Seats         = new List<BookingSeatItem> { new() { SeatId = seat, PatronCategoryId = childId } },
            PaymentMethod = "Sandbox",
        };

        await FluentActions.Awaiting(() => _sut.CreateBookingAsync(Guid.NewGuid(), request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*patron category*");
    }

    [Fact]
    public async Task CreateBookingAsync_RejectsAnyCategory_WhenRoomTypeHasNoRows()
    {
        var theaterId  = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var seatTypeId = Guid.NewGuid();
        var adultId    = Guid.NewGuid();
        var seat       = Guid.NewGuid();

        SetupBaselineBookingMocks(theaterId, roomTypeId, ShowTimeId1, RoomId1, 0);
        _uowMock.Setup(u => u.SeatTypeStore.GetKindMapAsync(theaterId))
            .ReturnsAsync(new Dictionary<SeatKind, Guid> { [SeatKind.Standard] = seatTypeId });
        _uowMock.Setup(u => u.PatronCategoryStore.FindAsync(It.IsAny<Expression<Func<PatronCategory, bool>>>()))
            .ReturnsAsync(new List<PatronCategory> { new() { Id = adultId, TheaterId = theaterId, SeatTypeId = seatTypeId, Name = "Adult", Price = 90000, IsActive = true } });
        // No rows at all for this room type — it offers nothing, so even Adult is rejected.
        _uowMock.Setup(u => u.RoomTypePatronCategoryPriceStore.FindByPatronCategoriesAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<RoomTypePatronCategoryPrice>());
        SetupSeatsById(new Dictionary<Guid, Seat> { [seat] = new() { Id = seat, RoomId = RoomId1, RowName = "I", ColIndex = 1 } });

        var request = new CreateBookingRequest
        {
            ShowTimeId    = ShowTimeId1,
            RoomId        = RoomId1,
            Seats         = new List<BookingSeatItem> { new() { SeatId = seat, PatronCategoryId = adultId } },
            PaymentMethod = "Sandbox",
        };

        await FluentActions.Awaiting(() => _sut.CreateBookingAsync(Guid.NewGuid(), request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*patron category*");
    }

    [Fact]
    public async Task GetShowTimePricesAsync_DoublePrice_IndependentlyConfigured_NotDerivedFromStandard()
    {
        var theaterId    = Guid.NewGuid();
        var roomTypeId   = Guid.NewGuid();
        var standardType = Guid.NewGuid();
        var doubleType   = Guid.NewGuid();
        var adultStd     = Guid.NewGuid();
        var adultDbl     = Guid.NewGuid();

        SetupPricingContext(
            theaterId, roomTypeId, ShowTimeId1, RoomId1, basePrice: 0,
            startTime: new DateTime(2026, 3, 2, 19, 0, 0), projectionForm: ProjectionForm.TwoD,
            categories: new List<PatronCategory>
            {
                new() { Id = adultStd, TheaterId = theaterId, SeatTypeId = standardType, Name = "Adult", Price = 90000, IsActive = true },
                new() { Id = adultDbl, TheaterId = theaterId, SeatTypeId = doubleType,   Name = "Adult", Price = 170000, IsActive = true },
            },
            kindMap: new Dictionary<SeatKind, Guid> { [SeatKind.Standard] = standardType, [SeatKind.Double] = doubleType });

        var result = await _sut.GetShowTimePricesAsync(ShowTimeId1, RoomId1);

        result.Single(p => p.Kind == SeatKind.Standard).Price.Should().Be(90000);
        result.Single(p => p.Kind == SeatKind.Double).Price.Should().Be(170000);
    }

    // ── CreateBookingAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateBookingAsync_RejectsSeatHeldByAnotherConnection()
    {
        var userId   = Guid.NewGuid();
        var heldSeat = Guid.NewGuid();   // unique id so the static lock store isn't shared with other tests
        _sut.LockSeat(ShowTimeId1, RoomId1, heldSeat, "other-conn");

        _uowMock.Setup(u => u.ShowTimeStore.GetShowTimeRoomAsync(ShowTimeId1, RoomId1))
            .ReturnsAsync(new ShowTimeRoom { ShowTimeId = ShowTimeId1, RoomId = RoomId1, BasePrice = 100, Room = new Room { Id = RoomId1, Status = RoomStatus.Active } });
        _uowMock.Setup(u => u.RoomStore.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Room?)null);
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatIdsAsync(ShowTimeId1, RoomId1)).ReturnsAsync(new List<Guid>());
        SetupSeatsById(new Dictionary<Guid, Seat> { [heldSeat] = new() { Id = heldSeat, RoomId = RoomId1, RowName = "A", ColIndex = 1 } });

        var request = new CreateBookingRequest
        {
            ShowTimeId    = ShowTimeId1,
            RoomId        = RoomId1,
            Seats         = new List<BookingSeatItem> { new() { SeatId = heldSeat } },
            ConnectionId  = "my-conn",           // booker's own connection differs from the holder's
            PaymentMethod = "Sandbox",
        };

        await FluentActions.Awaiting(() => _sut.CreateBookingAsync(userId, request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*held by another user*");

        _seatNotificationsMock.Verify(
            n => n.NotifySeatsBookedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateBookingAsync_RejectsBlockedSeat()
    {
        var seat = Guid.NewGuid();
        SetupBaselineBookingMocks(Guid.NewGuid(), Guid.NewGuid(), ShowTimeId1, RoomId1, 100);
        SetupSeatsById(new Dictionary<Guid, Seat> { [seat] = new() { Id = seat, RoomId = RoomId1, RowName = "Z", ColIndex = 1, IsActive = false } });

        var request = new CreateBookingRequest
        {
            ShowTimeId = ShowTimeId1,
            RoomId = RoomId1,
            Seats = new List<BookingSeatItem> { new() { SeatId = seat, PatronCategoryId = Guid.NewGuid() } },
            PaymentMethod = "Sandbox",
        };

        await FluentActions.Awaiting(() => _sut.CreateBookingAsync(Guid.NewGuid(), request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*blocked*");
        _uowMock.Verify(u => u.InvoiceStore.CreateAsync(It.IsAny<Invoice>()), Times.Never);
    }

    [Fact]
    public async Task CreateBookingAsync_RejectsSeatFromAnotherRoom()
    {
        var seat = Guid.NewGuid();
        SetupBaselineBookingMocks(Guid.NewGuid(), Guid.NewGuid(), ShowTimeId1, RoomId1, 100);
        SetupSeatsById(new Dictionary<Guid, Seat> { [seat] = new() { Id = seat, RoomId = RoomId2, RowName = "Y", ColIndex = 1 } });

        var request = new CreateBookingRequest
        {
            ShowTimeId = ShowTimeId1,
            RoomId = RoomId1,
            Seats = new List<BookingSeatItem> { new() { SeatId = seat, PatronCategoryId = Guid.NewGuid() } },
            PaymentMethod = "Sandbox",
        };

        await FluentActions.Awaiting(() => _sut.CreateBookingAsync(Guid.NewGuid(), request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*does not belong to this room*");
    }

    [Theory]
    [InlineData(RoomStatus.Maintenance)]
    [InlineData(RoomStatus.Inactive)]
    public async Task CreateBookingAsync_RejectsRoomThatIsNotActive(RoomStatus status)
    {
        SetupBaselineBookingMocks(Guid.NewGuid(), Guid.NewGuid(), ShowTimeId1, RoomId1, 100);
        _uowMock.Setup(u => u.ShowTimeStore.GetShowTimeRoomAsync(ShowTimeId1, RoomId1))
            .ReturnsAsync(new ShowTimeRoom { ShowTimeId = ShowTimeId1, RoomId = RoomId1, BasePrice = 100, Room = new Room { Id = RoomId1, Status = status } });

        var request = new CreateBookingRequest
        {
            ShowTimeId = ShowTimeId1,
            RoomId = RoomId1,
            Seats = new List<BookingSeatItem> { new() { SeatId = Guid.NewGuid(), PatronCategoryId = Guid.NewGuid() } },
            PaymentMethod = "Sandbox",
        };

        await FluentActions.Awaiting(() => _sut.CreateBookingAsync(Guid.NewGuid(), request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*not open for booking*");
    }

    // ── Patron category pricing ─────────────────────────────────────────────────

    private void SetupBaselineBookingMocks(Guid theaterId, Guid roomTypeId, Guid showTimeId, Guid roomId, int basePrice)
    {
        _uowMock.Setup(u => u.ShowTimeStore.GetShowTimeRoomAsync(showTimeId, roomId))
            .ReturnsAsync(new ShowTimeRoom { ShowTimeId = showTimeId, RoomId = roomId, BasePrice = basePrice, Room = new Room { Id = roomId, TheaterId = theaterId, Status = RoomStatus.Active } });
        _uowMock.Setup(u => u.RoomStore.GetByIdAsync(roomId))
            .ReturnsAsync(new Room { Id = roomId, TheaterId = theaterId, RoomTypeId = roomTypeId });
        _uowMock.Setup(u => u.ShowTimeStore.GetByIdAsync(showTimeId))
            .ReturnsAsync(new ShowTime { Id = showTimeId, StartTime = new DateTime(2026, 3, 2, 19, 0, 0) });
        _uowMock.Setup(u => u.HolidayStore.FindAsync(It.IsAny<Expression<Func<Holiday, bool>>>()))
            .ReturnsAsync(new List<Holiday>());
        _uowMock.Setup(u => u.TimeSlotStore.FindAsync(It.IsAny<Expression<Func<TimeSlot, bool>>>()))
            .ReturnsAsync(new List<TimeSlot>());
        _uowMock.Setup(u => u.TicketPriceStore.FindAsync(It.IsAny<Expression<Func<TicketPrice, bool>>>()))
            .ReturnsAsync(new List<TicketPrice>());
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatIdsAsync(showTimeId, roomId)).ReturnsAsync(new List<Guid>());
        _uowMock.Setup(u => u.DiscountStore.GetActiveAutoApplyAsync(It.IsAny<DateTime>())).ReturnsAsync(new List<Discount>());
        _uowMock.Setup(u => u.InvoiceStore.CreateAsync(It.IsAny<Invoice>())).ReturnsAsync((Invoice i) => i);
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);
    }

    [Fact]
    public async Task CreateBookingAsync_UsesResolvedPatronCategoryPricePerSeat()
    {
        var theaterId   = Guid.NewGuid();
        var roomTypeId  = Guid.NewGuid();
        var standardType = Guid.NewGuid();
        var seatA       = Guid.NewGuid();
        var seatB       = Guid.NewGuid();
        var adultId     = Guid.NewGuid();
        var studentId   = Guid.NewGuid();

        SetupBaselineBookingMocks(theaterId, roomTypeId, ShowTimeId1, RoomId1, 0);
        _uowMock.Setup(u => u.SeatTypeStore.GetKindMapAsync(theaterId))
            .ReturnsAsync(new Dictionary<SeatKind, Guid> { [SeatKind.Standard] = standardType });
        var categories = new List<PatronCategory>
        {
            new() { Id = adultId,   TheaterId = theaterId, SeatTypeId = standardType, Name = "Adult",   Price = 100, IsActive = true },
            new() { Id = studentId, TheaterId = theaterId, SeatTypeId = standardType, Name = "Student", Price = 75,  IsActive = true },
        };
        _uowMock.Setup(u => u.PatronCategoryStore.FindAsync(It.IsAny<Expression<Func<PatronCategory, bool>>>()))
            .ReturnsAsync(categories);
        AllowAllCategories(roomTypeId, categories);
        SetupSeatsById(new Dictionary<Guid, Seat>
        {
            [seatA] = new() { Id = seatA, RoomId = RoomId1, RowName = "A", ColIndex = 1 },
            [seatB] = new() { Id = seatB, RoomId = RoomId1, RowName = "A", ColIndex = 2 },
        });

        var request = new CreateBookingRequest
        {
            ShowTimeId    = ShowTimeId1,
            RoomId        = RoomId1,
            Seats = new List<BookingSeatItem>
            {
                new() { SeatId = seatA, PatronCategoryId = adultId },
                new() { SeatId = seatB, PatronCategoryId = studentId },
            },
            PaymentMethod = "Sandbox",
        };

        var result = await _sut.CreateBookingAsync(Guid.NewGuid(), request);

        result.Tickets.Should().HaveCount(2);
        result.Tickets.First(t => t.SeatLabel == "A1").Price.Should().Be(100);
        result.Tickets.First(t => t.SeatLabel == "A1").PatronCategory.Should().Be("Adult");
        result.Tickets.First(t => t.SeatLabel == "A2").Price.Should().Be(75);
        result.Tickets.First(t => t.SeatLabel == "A2").PatronCategory.Should().Be("Student");
        result.TotalAmount.Should().Be(175);
        result.FinalAmount.Should().Be(175);
    }

    [Fact]
    public async Task CreateBookingAsync_RedeemsPoints_UsesTheaterLoyaltyOverrideRate()
    {
        var theaterId    = Guid.NewGuid();
        var roomTypeId   = Guid.NewGuid();
        var standardType = Guid.NewGuid();
        var seatA        = Guid.NewGuid();
        var adultId      = Guid.NewGuid();
        var customerId   = Guid.NewGuid();

        SetupBaselineBookingMocks(theaterId, roomTypeId, ShowTimeId1, RoomId1, 0);
        _uowMock.Setup(u => u.SeatTypeStore.GetKindMapAsync(theaterId))
            .ReturnsAsync(new Dictionary<SeatKind, Guid> { [SeatKind.Standard] = standardType });
        var categories = new List<PatronCategory>
        {
            new() { Id = adultId, TheaterId = theaterId, SeatTypeId = standardType, Name = "Adult", Price = 100000, IsActive = true },
        };
        _uowMock.Setup(u => u.PatronCategoryStore.FindAsync(It.IsAny<Expression<Func<PatronCategory, bool>>>()))
            .ReturnsAsync(categories);
        AllowAllCategories(roomTypeId, categories);
        SetupSeatsById(new Dictionary<Guid, Seat> { [seatA] = new() { Id = seatA, RoomId = RoomId1, RowName = "A", ColIndex = 1 } });

        var customer = new User { Id = customerId, Points = 50 };
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(customerId)).ReturnsAsync(customer);
        _uowMock.Setup(u => u.UserStore.UpdateAsync(customer)).ReturnsAsync(customer);
        // Theater A overrides the global default: 1 point = 2,000 VND here.
        _uowMock.Setup(u => u.LoyaltySettingStore.GetOverrideAsync(theaterId))
            .ReturnsAsync(new LoyaltySetting { TheaterId = theaterId, PointsPerUnit = 1000, PointValueVnd = 2000 });

        var request = new CreateBookingRequest
        {
            ShowTimeId      = ShowTimeId1,
            RoomId          = RoomId1,
            Seats           = new List<BookingSeatItem> { new() { SeatId = seatA, PatronCategoryId = adultId } },
            PointsToRedeem  = 5,
            PaymentMethod   = "Sandbox",
        };

        var result = await _sut.CreateBookingAsync(customerId, request);

        result.PointsRedeemed.Should().Be(5);
        result.DiscountAmount.Should().Be(10000);
        result.FinalAmount.Should().Be(90000);
        customer.Points.Should().Be(45);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_CreditsPoints_UsesTheaterLoyaltyOverrideRate()
    {
        var theaterId  = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var invoice = new Invoice
        {
            Id          = Guid.NewGuid(),
            UserId      = customerId,
            TheaterId   = theaterId,
            Status      = InvoiceStatus.Pending,
            FinalAmount = 100000,
            PaymentMethod = "Sandbox",
        };
        var customer = new User { Id = customerId, Points = 0 };
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(invoice)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(customerId)).ReturnsAsync(customer);
        _uowMock.Setup(u => u.UserStore.UpdateAsync(customer)).ReturnsAsync(customer);
        _uowMock.Setup(u => u.MemberShipStore.FindAsync(It.IsAny<Expression<Func<MemberShip, bool>>>()))
            .ReturnsAsync(new List<MemberShip>());
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        // Theater A overrides the global default: 1 point per 500 VND earned here.
        _uowMock.Setup(u => u.LoyaltySettingStore.GetOverrideAsync(theaterId))
            .ReturnsAsync(new LoyaltySetting { TheaterId = theaterId, PointsPerUnit = 500, PointValueVnd = 500 });

        var confirmed = await _sut.ConfirmPaymentAsync(customerId, invoice.Id, "SANDBOX-REF");

        confirmed.Should().BeTrue();
        customer.Points.Should().Be(200); // 100000 / 500
    }

    [Fact]
    public async Task ConfirmPaymentAsync_CreditsPoints_FallsBackToGlobalDefaultWhenNoOverride()
    {
        var theaterId  = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var invoice = new Invoice
        {
            Id          = Guid.NewGuid(),
            UserId      = customerId,
            TheaterId   = theaterId,
            Status      = InvoiceStatus.Pending,
            FinalAmount = 100000,
            PaymentMethod = "Sandbox",
        };
        var customer = new User { Id = customerId, Points = 0 };
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(invoice)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(customerId)).ReturnsAsync(customer);
        _uowMock.Setup(u => u.UserStore.UpdateAsync(customer)).ReturnsAsync(customer);
        _uowMock.Setup(u => u.MemberShipStore.FindAsync(It.IsAny<Expression<Func<MemberShip, bool>>>()))
            .ReturnsAsync(new List<MemberShip>());
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        // No theater override and no global row configured -> LoyaltyRates' hardcoded 1000/1000 fallback.
        _uowMock.Setup(u => u.LoyaltySettingStore.GetOverrideAsync(theaterId)).ReturnsAsync((LoyaltySetting?)null);
        _uowMock.Setup(u => u.LoyaltySettingStore.GetGlobalAsync()).ReturnsAsync((LoyaltySetting?)null);

        var confirmed = await _sut.ConfirmPaymentAsync(customerId, invoice.Id, "SANDBOX-REF");

        confirmed.Should().BeTrue();
        customer.Points.Should().Be(100); // 100000 / 1000 (hardcoded default)
    }

    [Fact]
    public async Task CreateBookingAsync_RejectsSeatWithNoPatronCategory()
    {
        var theaterId  = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var seat       = Guid.NewGuid();

        SetupBaselineBookingMocks(theaterId, roomTypeId, ShowTimeId1, RoomId1, 100);
        SetupSeatsById(new Dictionary<Guid, Seat> { [seat] = new() { Id = seat, RoomId = RoomId1, RowName = "B", ColIndex = 1 } });

        var request = new CreateBookingRequest
        {
            ShowTimeId    = ShowTimeId1,
            RoomId        = RoomId1,
            Seats         = new List<BookingSeatItem> { new() { SeatId = seat, PatronCategoryId = null } },
            PaymentMethod = "Sandbox",
        };

        await FluentActions.Awaiting(() => _sut.CreateBookingAsync(Guid.NewGuid(), request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*patron category is required*");
    }

    [Fact]
    public async Task CreateBookingAsync_RejectsUnknownPatronCategory()
    {
        var theaterId  = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var seat       = Guid.NewGuid();

        SetupBaselineBookingMocks(theaterId, roomTypeId, ShowTimeId1, RoomId1, 100);
        SetupSeatsById(new Dictionary<Guid, Seat> { [seat] = new() { Id = seat, RoomId = RoomId1, RowName = "C", ColIndex = 1 } });
        _uowMock.Setup(u => u.PatronCategoryStore.FindAsync(It.IsAny<Expression<Func<PatronCategory, bool>>>()))
            .ReturnsAsync(new List<PatronCategory>());

        var request = new CreateBookingRequest
        {
            ShowTimeId    = ShowTimeId1,
            RoomId        = RoomId1,
            Seats         = new List<BookingSeatItem> { new() { SeatId = seat, PatronCategoryId = Guid.NewGuid() } },
            PaymentMethod = "Sandbox",
        };

        await FluentActions.Awaiting(() => _sut.CreateBookingAsync(Guid.NewGuid(), request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*patron category*");
    }

    [Fact]
    public async Task CreateBookingAsync_RejectsInactivePatronCategory()
    {
        var theaterId   = Guid.NewGuid();
        var roomTypeId  = Guid.NewGuid();
        var seat        = Guid.NewGuid();
        var categoryId  = Guid.NewGuid();

        SetupBaselineBookingMocks(theaterId, roomTypeId, ShowTimeId1, RoomId1, 100);
        SetupSeatsById(new Dictionary<Guid, Seat> { [seat] = new() { Id = seat, RoomId = RoomId1, RowName = "D", ColIndex = 1 } });
        // Inactive categories are filtered out of the pricing context entirely (IsActive == true in
        // the query), so an inactive category id simply never resolves.
        _uowMock.Setup(u => u.PatronCategoryStore.FindAsync(It.IsAny<Expression<Func<PatronCategory, bool>>>()))
            .ReturnsAsync(new List<PatronCategory>());

        var request = new CreateBookingRequest
        {
            ShowTimeId    = ShowTimeId1,
            RoomId        = RoomId1,
            Seats         = new List<BookingSeatItem> { new() { SeatId = seat, PatronCategoryId = categoryId } },
            PaymentMethod = "Sandbox",
        };

        await FluentActions.Awaiting(() => _sut.CreateBookingAsync(Guid.NewGuid(), request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*patron category*");
    }

    [Fact]
    public async Task CreateBookingAsync_RejectsPatronCategoryNotEligibleForSeatKind()
    {
        var theaterId    = Guid.NewGuid();
        var roomTypeId   = Guid.NewGuid();
        var standardType = Guid.NewGuid();
        var doubleType   = Guid.NewGuid();
        var seat         = Guid.NewGuid();
        var studentId    = Guid.NewGuid();

        SetupBaselineBookingMocks(theaterId, roomTypeId, ShowTimeId1, RoomId1, 100);
        _uowMock.Setup(u => u.SeatTypeStore.GetKindMapAsync(theaterId))
            .ReturnsAsync(new Dictionary<SeatKind, Guid> { [SeatKind.Standard] = standardType, [SeatKind.Double] = doubleType });
        // Student only has a Standard row — no Double row exists, so this category cannot book a
        // double seat at all (this IS the entire eligibility rule).
        var categories = new List<PatronCategory> { new() { Id = studentId, TheaterId = theaterId, SeatTypeId = standardType, Name = "Student", Price = 65, IsActive = true } };
        _uowMock.Setup(u => u.PatronCategoryStore.FindAsync(It.IsAny<Expression<Func<PatronCategory, bool>>>()))
            .ReturnsAsync(categories);
        AllowAllCategories(roomTypeId, categories);
        var groupId = Guid.NewGuid();
        SetupSeatsById(new Dictionary<Guid, Seat> { [seat] = new() { Id = seat, RoomId = RoomId1, RowName = "E", ColIndex = 1, SeatGroupId = groupId } });
        _uowMock.Setup(u => u.SeatStore.FindAsync(It.IsAny<Expression<Func<Seat, bool>>>()))
            .ReturnsAsync(new List<Seat> { new() { Id = seat, RoomId = RoomId1, RowName = "E", ColIndex = 1, SeatGroupId = groupId } });

        var request = new CreateBookingRequest
        {
            ShowTimeId    = ShowTimeId1,
            RoomId        = RoomId1,
            Seats         = new List<BookingSeatItem> { new() { SeatId = seat, PatronCategoryId = studentId } },
            PaymentMethod = "Sandbox",
        };

        await FluentActions.Awaiting(() => _sut.CreateBookingAsync(Guid.NewGuid(), request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*not available for the selected patron category*");
    }

    [Fact]
    public async Task CreateBookingAsync_AllowsDoubleSeat_WhenCategoryHasDoubleRow()
    {
        var theaterId    = Guid.NewGuid();
        var roomTypeId   = Guid.NewGuid();
        var doubleType   = Guid.NewGuid();
        var seatA        = Guid.NewGuid();
        var seatB        = Guid.NewGuid();
        var adultId      = Guid.NewGuid();
        var groupId      = Guid.NewGuid();

        SetupBaselineBookingMocks(theaterId, roomTypeId, ShowTimeId1, RoomId1, 0);
        _uowMock.Setup(u => u.SeatTypeStore.GetKindMapAsync(theaterId))
            .ReturnsAsync(new Dictionary<SeatKind, Guid> { [SeatKind.Double] = doubleType });
        var categories = new List<PatronCategory> { new() { Id = adultId, TheaterId = theaterId, SeatTypeId = doubleType, Name = "Adult", Price = 170, IsActive = true } };
        _uowMock.Setup(u => u.PatronCategoryStore.FindAsync(It.IsAny<Expression<Func<PatronCategory, bool>>>()))
            .ReturnsAsync(categories);
        AllowAllCategories(roomTypeId, categories);
        SetupSeatsById(new Dictionary<Guid, Seat>
        {
            [seatA] = new() { Id = seatA, RoomId = RoomId1, RowName = "F", ColIndex = 1, SeatGroupId = groupId },
            [seatB] = new() { Id = seatB, RoomId = RoomId1, RowName = "F", ColIndex = 2, SeatGroupId = groupId },
        });
        _uowMock.Setup(u => u.SeatStore.FindAsync(It.IsAny<Expression<Func<Seat, bool>>>()))
            .ReturnsAsync(new List<Seat>
            {
                new() { Id = seatA, RoomId = RoomId1, RowName = "F", ColIndex = 1, SeatGroupId = groupId },
                new() { Id = seatB, RoomId = RoomId1, RowName = "F", ColIndex = 2, SeatGroupId = groupId },
            });

        var request = new CreateBookingRequest
        {
            ShowTimeId = ShowTimeId1,
            RoomId     = RoomId1,
            Seats = new List<BookingSeatItem>
            {
                new() { SeatId = seatA, PatronCategoryId = adultId },
                new() { SeatId = seatB, PatronCategoryId = adultId },
            },
            PaymentMethod = "Sandbox",
        };

        var result = await _sut.CreateBookingAsync(Guid.NewGuid(), request);

        // 170 is the price for the whole couple seat, split across its two physical tickets.
        result.Tickets.Should().HaveCount(2);
        result.Tickets.Should().AllSatisfy(t => t.SeatType.Should().Be("Double"));
        result.Tickets.Sum(t => t.Price).Should().Be(170);
        result.TotalAmount.Should().Be(170);
        // Both halves of the double seat share one row-position number — not "F1"/"F2".
        result.Tickets.Should().AllSatisfy(t => t.SeatLabel.Should().Be("F1"));
    }

    [Fact]
    public async Task CreateBookingAsync_FreezesSeatLabel_DoubleSeatSharesOneNumberWithFollowingSingle()
    {
        // Row C: single (col 1), double (cols 2-3), single (col 4) — buying the double plus the
        // trailing single must freeze "C2" (both double halves) and "C3" (not "C4").
        var theaterId  = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var doubleType = Guid.NewGuid();
        var standardType = Guid.NewGuid();
        var otherSeat  = Guid.NewGuid();
        var seatA      = Guid.NewGuid();
        var seatB      = Guid.NewGuid();
        var seatD      = Guid.NewGuid();
        var adultDoubleId = Guid.NewGuid();
        var adultStandardId = Guid.NewGuid();
        var groupId    = Guid.NewGuid();

        SetupBaselineBookingMocks(theaterId, roomTypeId, ShowTimeId1, RoomId1, 0);
        _uowMock.Setup(u => u.SeatTypeStore.GetKindMapAsync(theaterId))
            .ReturnsAsync(new Dictionary<SeatKind, Guid> { [SeatKind.Double] = doubleType, [SeatKind.Standard] = standardType });
        var categories = new List<PatronCategory>
        {
            new() { Id = adultDoubleId, TheaterId = theaterId, SeatTypeId = doubleType, Name = "Adult", Price = 170, IsActive = true },
            new() { Id = adultStandardId, TheaterId = theaterId, SeatTypeId = standardType, Name = "Adult", Price = 90, IsActive = true },
        };
        _uowMock.Setup(u => u.PatronCategoryStore.FindAsync(It.IsAny<Expression<Func<PatronCategory, bool>>>()))
            .ReturnsAsync(categories);
        AllowAllCategories(roomTypeId, categories);
        // The whole room's layout (including the untouched col-1 single) is needed to number the row.
        var roomSeats = new Dictionary<Guid, Seat>
        {
            [otherSeat] = new() { Id = otherSeat, RoomId = RoomId1, RowName = "C", ColIndex = 1 },
            [seatA] = new() { Id = seatA, RoomId = RoomId1, RowName = "C", ColIndex = 2, SeatGroupId = groupId },
            [seatB] = new() { Id = seatB, RoomId = RoomId1, RowName = "C", ColIndex = 3, SeatGroupId = groupId },
            [seatD] = new() { Id = seatD, RoomId = RoomId1, RowName = "C", ColIndex = 4 },
        };
        SetupSeatsById(roomSeats);
        _uowMock.Setup(u => u.SeatStore.FindAsync(It.IsAny<Expression<Func<Seat, bool>>>()))
            .ReturnsAsync(new List<Seat> { roomSeats[seatA], roomSeats[seatB] });

        var request = new CreateBookingRequest
        {
            ShowTimeId = ShowTimeId1,
            RoomId     = RoomId1,
            Seats = new List<BookingSeatItem>
            {
                new() { SeatId = seatA, PatronCategoryId = adultDoubleId },
                new() { SeatId = seatB, PatronCategoryId = adultDoubleId },
                new() { SeatId = seatD, PatronCategoryId = adultStandardId },
            },
            PaymentMethod = "Sandbox",
        };

        var result = await _sut.CreateBookingAsync(Guid.NewGuid(), request);

        result.Tickets.Should().HaveCount(3);
        result.Tickets.Where(t => t.SeatType == "Double").Should().HaveCount(2).And.AllSatisfy(t => t.SeatLabel.Should().Be("C2"));
        result.Tickets.Should().ContainSingle(t => t.SeatType == "Single" && t.SeatLabel == "C3");
    }

    [Fact]
    public async Task CreateBookingAsync_RejectsBookingOnlyOneSeatOfAPair()
    {
        var theaterId    = Guid.NewGuid();
        var roomTypeId   = Guid.NewGuid();
        var doubleType   = Guid.NewGuid();
        var seatA        = Guid.NewGuid();
        var seatB        = Guid.NewGuid();
        var adultId      = Guid.NewGuid();
        var groupId      = Guid.NewGuid();

        SetupBaselineBookingMocks(theaterId, roomTypeId, ShowTimeId1, RoomId1, 0);
        _uowMock.Setup(u => u.SeatTypeStore.GetKindMapAsync(theaterId))
            .ReturnsAsync(new Dictionary<SeatKind, Guid> { [SeatKind.Double] = doubleType });
        _uowMock.Setup(u => u.PatronCategoryStore.FindAsync(It.IsAny<Expression<Func<PatronCategory, bool>>>()))
            .ReturnsAsync(new List<PatronCategory> { new() { Id = adultId, TheaterId = theaterId, SeatTypeId = doubleType, Name = "Adult", Price = 170, IsActive = true } });
        // Only seatA is requested, but the DB shows seatA+seatB share a group — seatB is missing.
        SetupSeatsById(new Dictionary<Guid, Seat> { [seatA] = new() { Id = seatA, RoomId = RoomId1, RowName = "G", ColIndex = 1, SeatGroupId = groupId } });
        _uowMock.Setup(u => u.SeatStore.FindAsync(It.IsAny<Expression<Func<Seat, bool>>>()))
            .ReturnsAsync(new List<Seat>
            {
                new() { Id = seatA, RoomId = RoomId1, RowName = "G", ColIndex = 1, SeatGroupId = groupId },
                new() { Id = seatB, RoomId = RoomId1, RowName = "G", ColIndex = 2, SeatGroupId = groupId },
            });

        var request = new CreateBookingRequest
        {
            ShowTimeId    = ShowTimeId1,
            RoomId        = RoomId1,
            Seats         = new List<BookingSeatItem> { new() { SeatId = seatA, PatronCategoryId = adultId } },
            PaymentMethod = "Sandbox",
        };

        await FluentActions.Awaiting(() => _sut.CreateBookingAsync(Guid.NewGuid(), request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*booked together with its partner*");
    }

    [Fact]
    public async Task CancelBookingAsync_WrongUser_ReturnsFalse()
    {
        var invoice = new Invoice
        {
            Id     = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = InvoiceStatus.Pending
        };
        var otherUserId = Guid.NewGuid();
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);

        var result = await _sut.CancelBookingAsync(otherUserId, invoice.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CancelBookingAsync_AlreadyPaid_ReturnsFalse()
    {
        var userId  = Guid.NewGuid();
        var invoice = new Invoice
        {
            Id     = Guid.NewGuid(),
            UserId = userId,
            Status = InvoiceStatus.Paid
        };
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);

        var result = await _sut.CancelBookingAsync(userId, invoice.Id);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CancelBookingAsync_ValidPending_CancelsAndReturnsTrue()
    {
        var userId  = Guid.NewGuid();
        var invoice = new Invoice
        {
            Id     = Guid.NewGuid(),
            UserId = userId,
            Status = InvoiceStatus.Pending
        };
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(invoice)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _sut.CancelBookingAsync(userId, invoice.Id);

        result.Should().BeTrue();
        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
    }

    [Fact]
    public void LockSeat_SeatBecomesLocked()
    {
        _sut.LockSeat(ShowTimeId1, RoomId1, SeatId10, "conn-abc");

        _sut.IsSeatLocked(ShowTimeId1, RoomId1, SeatId10).Should().BeTrue();
    }

    [Fact]
    public void UnlockSeat_SeatBecomesUnlocked()
    {
        _sut.LockSeat(ShowTimeId1, RoomId1, SeatId11, "conn-xyz");
        _sut.UnlockSeat(ShowTimeId1, RoomId1, SeatId11, "conn-xyz");

        _sut.IsSeatLocked(ShowTimeId1, RoomId1, SeatId11).Should().BeFalse();
    }

    [Fact]
    public void IsSeatLocked_ExcludeOwnConnection_ReturnsFalse()
    {
        _sut.LockSeat(ShowTimeId2, RoomId1, SeatId20, "my-conn");

        _sut.IsSeatLocked(ShowTimeId2, RoomId1, SeatId20, excludeConnectionId: "my-conn").Should().BeFalse();
    }

    [Fact]
    public void IsSeatLocked_OtherConnection_ReturnsTrue()
    {
        _sut.LockSeat(ShowTimeId2, RoomId1, SeatId21, "other-conn");

        _sut.IsSeatLocked(ShowTimeId2, RoomId1, SeatId21, excludeConnectionId: "my-conn").Should().BeTrue();
    }

    [Fact]
    public async Task CancelBookingAsync_RestoresReservedLoyaltyPoints()
    {
        var userId  = Guid.NewGuid();
        var user    = new User { Id = userId, Points = 5 };
        var invoice = new Invoice
        {
            Id             = Guid.NewGuid(),
            UserId         = userId,
            Status         = InvoiceStatus.Pending,
            PointsRedeemed = 10,
        };
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(invoice)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(userId)).ReturnsAsync(user);
        _uowMock.Setup(u => u.UserStore.UpdateAsync(user)).ReturnsAsync(user);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _sut.CancelBookingAsync(userId, invoice.Id);

        result.Should().BeTrue();
        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
        user.Points.Should().Be(15);
    }

    [Fact]
    public async Task CancelBookingAsync_RestoresGiftCardBalance()
    {
        var userId  = Guid.NewGuid();
        var card    = new GiftCard { Id = Guid.NewGuid(), Balance = 20000 };
        var invoice = new Invoice
        {
            Id             = Guid.NewGuid(),
            UserId         = userId,
            Status         = InvoiceStatus.Pending,
            GiftCardId     = card.Id,
            GiftCardAmount = 30000,
        };
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(invoice)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.GiftCardStore.GetByIdAsync(card.Id)).ReturnsAsync(card);
        _uowMock.Setup(u => u.GiftCardStore.UpdateAsync(card)).ReturnsAsync(card);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        await _sut.CancelBookingAsync(userId, invoice.Id);

        card.Balance.Should().Be(50000); // 20000 remaining + 30000 restored
    }

    [Fact]
    public async Task CancelBookingAsync_DeactivatesTicketsToFreeSeats()
    {
        var userId  = Guid.NewGuid();
        var invoice = new Invoice { Id = Guid.NewGuid(), UserId = userId, Status = InvoiceStatus.Pending };
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(invoice)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        await _sut.CancelBookingAsync(userId, invoice.Id);

        // Frees the seats at the DB unique-index level for multi-instance safety.
        _uowMock.Verify(u => u.InvoiceStore.DeactivateTicketsAsync(invoice.Id), Times.Once);
    }

    // ── Discount code validation ────────────────────────────────────────────────

    private Discount SetupDiscount(Action<Discount>? tweak = null)
    {
        var discount = new Discount
        {
            Id = Guid.NewGuid(), Code = "SUMMER25", Percent = 10, IsActive = true,
            StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(1),
            ApplyToAllTheaters = true,
        };
        tweak?.Invoke(discount);
        _uowMock.Setup(u => u.DiscountStore.GetByCodeAsync("SUMMER25")).ReturnsAsync(discount);
        _uowMock.Setup(u => u.RoomStore.GetByIdAsync(RoomId1)).ReturnsAsync(new Room { Id = RoomId1, TheaterId = Guid.NewGuid() });
        _uowMock.Setup(u => u.ShowTimeStore.GetByIdAsync(ShowTimeId1)).ReturnsAsync(new ShowTime { Id = ShowTimeId1, StartTime = DateTime.UtcNow });
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);
        return discount;
    }

    [Fact]
    public async Task ValidateDiscountCodeAsync_UsableCode_ReportsWhatItTakesOff()
    {
        SetupDiscount();

        var result = await _sut.ValidateDiscountCodeAsync(Guid.NewGuid(), " SUMMER25 ", RoomId1, ShowTimeId1, 200000);

        result.Valid.Should().BeTrue();
        result.DiscountAmount.Should().Be(20000);
    }

    [Fact]
    public async Task ValidateDiscountCodeAsync_CapsTheAmountAtMaxDiscount()
    {
        SetupDiscount(d => d.MaxDiscountAmount = 5000);

        var result = await _sut.ValidateDiscountCodeAsync(Guid.NewGuid(), "SUMMER25", RoomId1, ShowTimeId1, 200000);

        result.DiscountAmount.Should().Be(5000);
    }

    [Fact]
    public async Task ValidateDiscountCodeAsync_UnknownCode_IsInvalidWithoutThrowing()
    {
        _uowMock.Setup(u => u.DiscountStore.GetByCodeAsync("NOPE")).ReturnsAsync((Discount?)null);
        _uowMock.Setup(u => u.RoomStore.GetByIdAsync(RoomId1)).ReturnsAsync(new Room { Id = RoomId1, TheaterId = Guid.NewGuid() });
        _uowMock.Setup(u => u.ShowTimeStore.GetByIdAsync(ShowTimeId1)).ReturnsAsync(new ShowTime { Id = ShowTimeId1, StartTime = DateTime.UtcNow });

        var result = await _sut.ValidateDiscountCodeAsync(Guid.NewGuid(), "NOPE", RoomId1, ShowTimeId1, 200000);

        result.Valid.Should().BeFalse();
        result.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateDiscountCodeAsync_ExpiredOrExhaustedCode_IsInvalid()
    {
        SetupDiscount(d => d.EndDate = DateTime.UtcNow.AddDays(-1));
        (await _sut.ValidateDiscountCodeAsync(Guid.NewGuid(), "SUMMER25", RoomId1, ShowTimeId1, 200000)).Valid.Should().BeFalse();

        SetupDiscount(d => { d.MaxUsage = 3; d.UsedCount = 3; });
        (await _sut.ValidateDiscountCodeAsync(Guid.NewGuid(), "SUMMER25", RoomId1, ShowTimeId1, 200000)).Valid.Should().BeFalse();
    }

    // ── Food stock: deduct at booking, restore on cancel / expire ──────

    private static readonly Guid FoodTheaterId = Guid.NewGuid();

    private static FoodAndDrink Food(string name, double price, bool tracked, bool isCombo = false, Guid? theaterId = null, bool available = true)
    {
        return new FoodAndDrink
        {
            Id             = Guid.NewGuid(),
            TheaterId      = theaterId ?? FoodTheaterId,
            Name           = name,
            Price          = price,
            TrackInventory = tracked,
            IsCombo        = isCombo,
            IsAvailable    = available,
        };
    }

    /// <summary>Wires a one-seat booking in <see cref="FoodTheaterId"/> and the food store with the given
    /// items (stock calls succeed unless a test overrides them). Returns a request skeleton to add Foods to.</summary>
    private CreateBookingRequest SetupFoodBooking(params FoodAndDrink[] foods)
    {
        var roomTypeId   = Guid.NewGuid();
        var standardType = Guid.NewGuid();
        var seat         = Guid.NewGuid();
        var adultId      = Guid.NewGuid();

        SetupBaselineBookingMocks(FoodTheaterId, roomTypeId, ShowTimeId1, RoomId1, 0);
        _uowMock.Setup(u => u.SeatTypeStore.GetKindMapAsync(FoodTheaterId))
            .ReturnsAsync(new Dictionary<SeatKind, Guid> { [SeatKind.Standard] = standardType });
        var categories = new List<PatronCategory>
        {
            new() { Id = adultId, TheaterId = FoodTheaterId, SeatTypeId = standardType, Name = "Adult", Price = 100, IsActive = true },
        };
        _uowMock.Setup(u => u.PatronCategoryStore.FindAsync(It.IsAny<Expression<Func<PatronCategory, bool>>>())).ReturnsAsync(categories);
        AllowAllCategories(roomTypeId, categories);
        SetupSeatsById(new Dictionary<Guid, Seat> { [seat] = new() { Id = seat, RoomId = RoomId1, RowName = "A", ColIndex = 1 } });

        _uowMock.Setup(u => u.FoodAndDrinkStore.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids) => foods.Where(f => ids.Contains(f.Id)).ToDictionary(f => f.Id));
        _uowMock.Setup(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(It.IsAny<Guid>(), It.IsAny<int>())).ReturnsAsync(true);
        _uowMock.Setup(u => u.ComboItemStore.GetByCombosAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<ComboItem>());

        return new CreateBookingRequest
        {
            ShowTimeId    = ShowTimeId1,
            RoomId        = RoomId1,
            Seats         = new List<BookingSeatItem> { new() { SeatId = seat, PatronCategoryId = adultId } },
            PaymentMethod = "Sandbox",
        };
    }

    private List<StockMovement> CaptureMovements()
    {
        var captured = new List<StockMovement>();
        _uowMock.Setup(u => u.StockMovementStore.CreateRangeAsync(It.IsAny<List<StockMovement>>()))
            .Callback((List<StockMovement> m) => captured.AddRange(m))
            .ReturnsAsync((List<StockMovement> m) => m);
        return captured;
    }

    [Fact]
    public async Task CreateBookingAsync_UntrackedFood_MakesNoStockOrMovementCalls()
    {
        var popcorn = Food("Popcorn", 50, tracked: false);
        var request = SetupFoodBooking(popcorn);
        request.Foods.Add(new BookingFoodItem { FoodAndDrinkId = popcorn.Id, Quantity = 2 });

        var result = await _sut.CreateBookingAsync(Guid.NewGuid(), request);

        result.TotalAmount.Should().Be(200);
        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
        _uowMock.Verify(u => u.StockMovementStore.CreateRangeAsync(It.IsAny<List<StockMovement>>()), Times.Never);
    }

    [Fact]
    public async Task CreateBookingAsync_TrackedFoodInsufficientStock_ThrowsRollsBackAndCreatesNoInvoice()
    {
        var cola = Food("Cola", 30, tracked: true);
        var request = SetupFoodBooking(cola);
        _uowMock.Setup(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(cola.Id, -2)).ReturnsAsync(false);
        request.Foods.Add(new BookingFoodItem { FoodAndDrinkId = cola.Id, Quantity = 2 });

        await FluentActions.Awaiting(() => _sut.CreateBookingAsync(Guid.NewGuid(), request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*Cola*out of stock*");

        _uowMock.Verify(u => u.RollbackTransactionAsync(), Times.Once);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Never);
        _uowMock.Verify(u => u.InvoiceStore.CreateAsync(It.IsAny<Invoice>()), Times.Never);
    }

    [Fact]
    public async Task CreateBookingAsync_SameFoodOnTwoLines_MergesIntoOneRowAndOneStockCall()
    {
        var cola = Food("Cola", 30, tracked: true);
        var request = SetupFoodBooking(cola);
        Invoice? created = null;
        _uowMock.Setup(u => u.InvoiceStore.CreateAsync(It.IsAny<Invoice>()))
            .Callback((Invoice i) => created = i).ReturnsAsync((Invoice i) => i);
        request.Foods.Add(new BookingFoodItem { FoodAndDrinkId = cola.Id, Quantity = 1 });
        request.Foods.Add(new BookingFoodItem { FoodAndDrinkId = cola.Id, Quantity = 2 });

        var result = await _sut.CreateBookingAsync(Guid.NewGuid(), request);

        created!.InvoiceFoodAndDrinks.Should().ContainSingle()
            .Which.Quantity.Should().Be(3);
        result.TotalAmount.Should().Be(100 + 90);
        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(cola.Id, -3), Times.Once);
        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task CreateBookingAsync_Combo_DeductsOnlyTrackedComponentsAndRecordsSaleMovement()
    {
        var a     = Food("Cola", 10, tracked: true);
        var b     = Food("Straw", 1, tracked: false);
        var combo = Food("Combo 1", 80, tracked: false, isCombo: true);
        var request = SetupFoodBooking(a, b, combo);
        _uowMock.Setup(u => u.ComboItemStore.GetByCombosAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<ComboItem>
            {
                new() { ComboId = combo.Id, ComponentId = a.Id, Quantity = 1 },
                new() { ComboId = combo.Id, ComponentId = b.Id, Quantity = 2 },
            });
        var movements = CaptureMovements();
        Invoice? created = null;
        _uowMock.Setup(u => u.InvoiceStore.CreateAsync(It.IsAny<Invoice>()))
            .Callback((Invoice i) => created = i).ReturnsAsync((Invoice i) => i);
        request.Foods.Add(new BookingFoodItem { FoodAndDrinkId = combo.Id, Quantity = 2 });

        await _sut.CreateBookingAsync(Guid.NewGuid(), request);

        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(a.Id, -2), Times.Once);
        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(b.Id, It.IsAny<int>()), Times.Never);
        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(combo.Id, It.IsAny<int>()), Times.Never);
        var sale = movements.Should().ContainSingle().Subject;
        sale.Type.Should().Be(StockMovementType.Sale);
        sale.FoodAndDrinkId.Should().Be(a.Id);
        sale.Quantity.Should().Be(-2);
        sale.InvoiceId.Should().Be(created!.Id);
        sale.TheaterId.Should().Be(FoodTheaterId);
        var line = created.InvoiceFoodAndDrinks.Should().ContainSingle().Subject;
        line.FoodAndDrinkId.Should().Be(combo.Id);
        line.Quantity.Should().Be(2);
    }

    [Fact]
    public async Task CreateBookingAsync_PlainFoodPlusComboContainingIt_CombinesIntoOneStockCall()
    {
        var a     = Food("Cola", 10, tracked: true);
        var combo = Food("Combo 1", 80, tracked: false, isCombo: true);
        var request = SetupFoodBooking(a, combo);
        _uowMock.Setup(u => u.ComboItemStore.GetByCombosAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<ComboItem> { new() { ComboId = combo.Id, ComponentId = a.Id, Quantity = 1 } });
        var movements = CaptureMovements();
        request.Foods.Add(new BookingFoodItem { FoodAndDrinkId = a.Id, Quantity = 1 });
        request.Foods.Add(new BookingFoodItem { FoodAndDrinkId = combo.Id, Quantity = 2 });

        await _sut.CreateBookingAsync(Guid.NewGuid(), request);

        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(a.Id, -3), Times.Once);
        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Once);
        movements.Should().ContainSingle().Which.Quantity.Should().Be(-3);
    }

    [Fact]
    public async Task CreateBookingAsync_ComboWithShortSecondComponent_ThrowsNamingComboAndCommitsNothing()
    {
        var a     = Food("Cola", 10, tracked: true);
        var b     = Food("Chips", 10, tracked: true);
        var combo = Food("Combo 1", 80, tracked: false, isCombo: true);
        var request = SetupFoodBooking(a, b, combo);
        _uowMock.Setup(u => u.ComboItemStore.GetByCombosAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<ComboItem>
            {
                new() { ComboId = combo.Id, ComponentId = a.Id, Quantity = 1 },
                new() { ComboId = combo.Id, ComponentId = b.Id, Quantity = 1 },
            });
        // Whichever component sorts second is the short one; stock applies in ascending id order.
        var second = new[] { a.Id, b.Id }.OrderBy(id => id).Last();
        _uowMock.Setup(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(second, -1)).ReturnsAsync(false);
        request.Foods.Add(new BookingFoodItem { FoodAndDrinkId = combo.Id, Quantity = 1 });

        await FluentActions.Awaiting(() => _sut.CreateBookingAsync(Guid.NewGuid(), request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*Combo 1*out of stock*");

        _uowMock.Verify(u => u.RollbackTransactionAsync(), Times.Once);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Never);
        _uowMock.Verify(u => u.InvoiceStore.CreateAsync(It.IsAny<Invoice>()), Times.Never);
        _uowMock.Verify(u => u.StockMovementStore.CreateRangeAsync(It.IsAny<List<StockMovement>>()), Times.Never);
    }

    [Fact]
    public async Task CreateBookingAsync_FoodFromAnotherTheater_Throws()
    {
        var foreign = Food("Foreign", 10, tracked: true, theaterId: Guid.NewGuid());
        var request = SetupFoodBooking(foreign);
        request.Foods.Add(new BookingFoodItem { FoodAndDrinkId = foreign.Id, Quantity = 1 });

        await FluentActions.Awaiting(() => _sut.CreateBookingAsync(Guid.NewGuid(), request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*not available at this theater*");

        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CreateBookingAsync_UnavailableFood_Throws()
    {
        var off = Food("Off the menu", 10, tracked: true, available: false);
        var request = SetupFoodBooking(off);
        request.Foods.Add(new BookingFoodItem { FoodAndDrinkId = off.Id, Quantity = 1 });

        await FluentActions.Awaiting(() => _sut.CreateBookingAsync(Guid.NewGuid(), request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*no longer available*");
    }

    private (Invoice Invoice, FoodAndDrink Food) SetupCancellableInvoiceWithSale(Guid userId, int netSold, bool tracked = true)
    {
        var invoice = new Invoice { Id = Guid.NewGuid(), UserId = userId, Status = InvoiceStatus.Pending };
        var cola = Food("Cola", 10, tracked);
        _uowMock.Setup(u => u.InvoiceStore.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(invoice)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _uowMock.Setup(u => u.StockMovementStore.GetNetSaleQuantitiesAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<StockNetQuantity> { new(invoice.Id, cola.Id, -netSold) });
        _uowMock.Setup(u => u.FoodAndDrinkStore.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, FoodAndDrink> { [cola.Id] = cola });
        _uowMock.Setup(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(It.IsAny<Guid>(), It.IsAny<int>())).ReturnsAsync(true);
        return (invoice, cola);
    }

    [Fact]
    public async Task CancelBookingAsync_RestocksNetSoldQuantityFromLedger_NotFromRecipe()
    {
        var userId = Guid.NewGuid();
        var (invoice, cola) = SetupCancellableInvoiceWithSale(userId, netSold: 3);
        var movements = CaptureMovements();

        (await _sut.CancelBookingAsync(userId, invoice.Id)).Should().BeTrue();

        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(cola.Id, 3), Times.Once);
        _uowMock.Verify(u => u.ComboItemStore.GetByCombosAsync(It.IsAny<IReadOnlyCollection<Guid>>()), Times.Never);
        _uowMock.Verify(u => u.StockMovementStore.GetNetSaleQuantitiesAsync(It.IsAny<IReadOnlyCollection<Guid>>()), Times.Once);
        var reversal = movements.Should().ContainSingle().Subject;
        reversal.Type.Should().Be(StockMovementType.SaleReversal);
        reversal.Quantity.Should().Be(3);
        reversal.InvoiceId.Should().Be(invoice.Id);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
    }

    [Fact]
    public async Task CancelBookingAsync_SecondRestockFindsNetZeroAndIsNoOp()
    {
        var userId = Guid.NewGuid();
        var (invoice, cola) = SetupCancellableInvoiceWithSale(userId, netSold: 0);

        // Net 0 (a SaleReversal already recorded) is what the ledger reports after a first restock.
        await _sut.CancelBookingAsync(userId, invoice.Id);

        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(cola.Id, It.IsAny<int>()), Times.Never);
        _uowMock.Verify(u => u.StockMovementStore.CreateRangeAsync(It.IsAny<List<StockMovement>>()), Times.Never);
    }

    [Fact]
    public async Task CancelBookingAsync_ItemNoLongerTracked_IsSkipped()
    {
        var userId = Guid.NewGuid();
        var (invoice, cola) = SetupCancellableInvoiceWithSale(userId, netSold: 2, tracked: false);

        await _sut.CancelBookingAsync(userId, invoice.Id);

        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(cola.Id, It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ExpireStalePendingBookingsAsync_RestocksWithOneBatchedLedgerCall()
    {
        var invoices = new List<Invoice>
        {
            new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Status = InvoiceStatus.Pending },
            new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Status = InvoiceStatus.Pending },
        };
        var cola = Food("Cola", 10, tracked: true);
        _uowMock.Setup(u => u.InvoiceStore.GetStalePendingAsync(It.IsAny<DateTime>())).ReturnsAsync(invoices);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(It.IsAny<Invoice>())).ReturnsAsync((Invoice i) => i);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _uowMock.Setup(u => u.StockMovementStore.GetNetSaleQuantitiesAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<StockNetQuantity>
            {
                new(invoices[0].Id, cola.Id, -2),
                new(invoices[1].Id, cola.Id, -1),
            });
        _uowMock.Setup(u => u.FoodAndDrinkStore.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, FoodAndDrink> { [cola.Id] = cola });
        _uowMock.Setup(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(It.IsAny<Guid>(), It.IsAny<int>())).ReturnsAsync(true);
        var movements = CaptureMovements();

        var count = await _sut.ExpireStalePendingBookingsAsync(TimeSpan.FromMinutes(15));

        count.Should().Be(2);
        _uowMock.Verify(u => u.StockMovementStore.GetNetSaleQuantitiesAsync(It.IsAny<IReadOnlyCollection<Guid>>()), Times.Once);
        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(cola.Id, 2), Times.Once);
        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(cola.Id, 1), Times.Once);
        movements.Should().HaveCount(2).And.OnlyContain(m => m.Type == StockMovementType.SaleReversal && m.UserId == null);
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
    }

    // ── PromotionProgram auto-apply pricing ─────────────────────────────────────

    /// <summary>Books one Adult-category standard seat at the given list price, with the given
    /// auto-apply PromotionProgram candidates in scope. Monday 2026-03-02 19:00 (see SetupBaselineBookingMocks).</summary>
    private async Task<BookingResultDTO> BookOneSeatWithProgramsAsync(
        double listPrice, List<PromotionProgram> candidates, Guid? customerId = null, User? customer = null,
        List<Discount>? autoApplyDiscounts = null, List<Holiday>? holidays = null)
    {
        var theaterId    = Guid.NewGuid();
        var roomTypeId   = Guid.NewGuid();
        var standardType = Guid.NewGuid();
        var seatA        = Guid.NewGuid();
        var adultId      = Guid.NewGuid();

        SetupBaselineBookingMocks(theaterId, roomTypeId, ShowTimeId1, RoomId1, 0);
        _uowMock.Setup(u => u.SeatTypeStore.GetKindMapAsync(theaterId))
            .ReturnsAsync(new Dictionary<SeatKind, Guid> { [SeatKind.Standard] = standardType });
        var categories = new List<PatronCategory>
        {
            new() { Id = adultId, TheaterId = theaterId, SeatTypeId = standardType, Name = "Adult", Price = listPrice, IsActive = true },
        };
        _uowMock.Setup(u => u.PatronCategoryStore.FindAsync(It.IsAny<Expression<Func<PatronCategory, bool>>>()))
            .ReturnsAsync(categories);
        AllowAllCategories(roomTypeId, categories);
        SetupSeatsById(new Dictionary<Guid, Seat> { [seatA] = new() { Id = seatA, RoomId = RoomId1, RowName = "A", ColIndex = 1 } });
        // These must be (re)configured after SetupBaselineBookingMocks above, which sets its own
        // (empty) defaults for the same members — a setup made before calling this helper would
        // otherwise be silently overridden by that call's defaults.
        _uowMock.Setup(u => u.PromotionProgramStore.GetAutoApplyCandidatesAsync(It.IsAny<DateOnly>())).ReturnsAsync(candidates);
        if (autoApplyDiscounts != null)
        {
            _uowMock.Setup(u => u.DiscountStore.GetActiveAutoApplyAsync(It.IsAny<DateTime>())).ReturnsAsync(autoApplyDiscounts);
        }
        if (holidays != null)
        {
            _uowMock.Setup(u => u.HolidayStore.FindAsync(It.IsAny<Expression<Func<Holiday, bool>>>())).ReturnsAsync(holidays);
        }
        if (customer != null)
        {
            _uowMock.Setup(u => u.UserStore.GetByIdAsync(customerId!.Value)).ReturnsAsync(customer);
        }

        var request = new CreateBookingRequest
        {
            ShowTimeId = ShowTimeId1,
            RoomId     = RoomId1,
            Seats = new List<BookingSeatItem> { new() { SeatId = seatA, PatronCategoryId = adultId } },
            PaymentMethod = "Sandbox",
        };

        return await _sut.CreateBookingAsync(customerId ?? Guid.NewGuid(), request);
    }

    private static PromotionProgram FixedPriceProgram(double price, int displayOrder = 0, PromotionEligibility eligibility = PromotionEligibility.Everyone, Guid? minMemberShipId = null) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Happy Hour",
        Mechanic = PromotionMechanic.FixedPrice,
        Eligibility = eligibility,
        MinMemberShipId = minMemberShipId,
        IsPublished = true,
        AutoApply = true,
        DisplayOrder = displayOrder,
        StartDate = new DateTime(2026, 1, 1),
        EndDate = new DateTime(2026, 12, 31),
        Prices = new List<PromotionProgramPrice> { new() { Price = price } },
        Schedules = new List<PromotionProgramSchedule>(),
    };

    [Fact]
    public async Task CreateBookingAsync_FixedPriceEveryoneProgram_AppliesAutomaticallyWithNoCode()
    {
        var program = FixedPriceProgram(price: 45000);

        var result = await BookOneSeatWithProgramsAsync(listPrice: 80000, candidates: new List<PromotionProgram> { program });

        result.FinalAmount.Should().Be(45000);
        result.AppliedPromotionTitle.Should().Be("Happy Hour");
    }

    [Fact]
    public async Task CreateBookingAsync_FixedPriceAboveListPrice_GivesNoSaving()
    {
        var program = FixedPriceProgram(price: 999999);

        var result = await BookOneSeatWithProgramsAsync(listPrice: 80000, candidates: new List<PromotionProgram> { program });

        result.FinalAmount.Should().Be(80000);
        result.AppliedPromotionTitle.Should().BeNull();
    }

    [Fact]
    public async Task CreateBookingAsync_MembersProgram_AppliesOnlyToQualifyingTier()
    {
        var goldTierId = Guid.NewGuid();
        _uowMock.Setup(u => u.MemberShipStore.FindAsync(It.IsAny<Expression<Func<MemberShip, bool>>>()))
            .ReturnsAsync(new List<MemberShip> { new() { Id = goldTierId, Name = "Gold", MinPoints = 1000 } });
        var program = FixedPriceProgram(price: 45000, eligibility: PromotionEligibility.Members, minMemberShipId: goldTierId);

        var customerId = Guid.NewGuid();
        var goldUser = new User { Id = customerId, MemberShipId = goldTierId };
        _uowMock.Setup(u => u.MemberShipStore.GetByIdAsync(goldTierId)).ReturnsAsync(new MemberShip { Id = goldTierId, Name = "Gold", MinPoints = 1000 });

        var result = await BookOneSeatWithProgramsAsync(listPrice: 80000, candidates: new List<PromotionProgram> { program }, customerId: customerId, customer: goldUser);

        result.FinalAmount.Should().Be(45000);
    }

    [Fact]
    public async Task CreateBookingAsync_MembersProgram_DoesNotApplyToAnonymousWalkIn()
    {
        var goldTierId = Guid.NewGuid();
        _uowMock.Setup(u => u.MemberShipStore.FindAsync(It.IsAny<Expression<Func<MemberShip, bool>>>()))
            .ReturnsAsync(new List<MemberShip> { new() { Id = goldTierId, Name = "Gold", MinPoints = 1000 } });
        var program = FixedPriceProgram(price: 45000, eligibility: PromotionEligibility.Members, minMemberShipId: goldTierId);

        // No customer set up at all (UserStore.GetByIdAsync defaults to null — see SetupBaselineBookingMocks).
        var result = await BookOneSeatWithProgramsAsync(listPrice: 80000, candidates: new List<PromotionProgram> { program });

        result.FinalAmount.Should().Be(80000);
    }

    [Fact]
    public async Task CreateBookingAsync_InfoOrStudentProgram_NeverChangesPrice()
    {
        var infoProgram = FixedPriceProgram(price: 1);
        infoProgram.Mechanic = PromotionMechanic.Info;

        var result = await BookOneSeatWithProgramsAsync(listPrice: 80000, candidates: new List<PromotionProgram> { infoProgram });

        result.FinalAmount.Should().Be(80000);
    }

    [Fact]
    public async Task CreateBookingAsync_AutoApplyDiscountCheaperThanProgram_ChargesDiscountNotProgram()
    {
        var program = FixedPriceProgram(price: 70000); // saves 10,000 off 80,000
        var discount = new Discount
        {
            Id = Guid.NewGuid(), Percent = 50, IsActive = true, AutoApply = true, ApplyToAllTheaters = true,
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), DiscountTheaters = new List<DiscountTheater>(),
        };

        var result = await BookOneSeatWithProgramsAsync(listPrice: 80000, candidates: new List<PromotionProgram> { program },
            autoApplyDiscounts: new List<Discount> { discount });

        result.FinalAmount.Should().Be(40000); // 50% off 80,000 beats the program's 70,000
        result.AppliedPromotionTitle.Should().BeNull();
    }

    [Fact]
    public async Task CreateBookingAsync_ProgramCheaperThanAutoApplyDiscount_ChargesProgramNotDiscount()
    {
        var program = FixedPriceProgram(price: 20000); // saves 60,000 off 80,000
        var discount = new Discount
        {
            Id = Guid.NewGuid(), Percent = 10, IsActive = true, AutoApply = true, ApplyToAllTheaters = true,
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), DiscountTheaters = new List<DiscountTheater>(),
        };

        var result = await BookOneSeatWithProgramsAsync(listPrice: 80000, candidates: new List<PromotionProgram> { program },
            autoApplyDiscounts: new List<Discount> { discount });

        result.FinalAmount.Should().Be(20000);
        result.AppliedPromotionTitle.Should().Be("Happy Hour");
    }

    [Fact]
    public async Task CreateBookingAsync_ProgramExcludesHolidays_NotAppliedOnHolidayShowtime()
    {
        var program = FixedPriceProgram(price: 45000);
        program.ExcludeHolidays = true;
        var holidays = new List<Holiday> { new() { Date = DateOnly.FromDateTime(new DateTime(2026, 3, 2)), PriceMultiplier = 1.5 } };

        var result = await BookOneSeatWithProgramsAsync(listPrice: 80000, candidates: new List<PromotionProgram> { program }, holidays: holidays);

        // The holiday's own 1.5x price multiplier still applies to the base ticket price (an existing,
        // unrelated pricing factor) — only the program's own discount is excluded by ExcludeHolidays.
        result.FinalAmount.Should().Be(120000);
    }

    [Fact]
    public async Task CreateBookingAsync_ProgramScheduleRestrictedToOtherDay_NotApplied()
    {
        var program = FixedPriceProgram(price: 45000);
        // SetupBaselineBookingMocks' showtime is Monday 2026-03-02; restrict the program to Tuesday only (bit 2).
        program.Schedules = new List<PromotionProgramSchedule> { new() { DaysOfWeekMask = 1 << (int)DayOfWeek.Tuesday } };

        var result = await BookOneSeatWithProgramsAsync(listPrice: 80000, candidates: new List<PromotionProgram> { program });

        result.FinalAmount.Should().Be(80000);
    }

    [Fact]
    public async Task CreateBookingAsync_ProgramChannelRestrictedToCounterOnly_NotAppliedOnline()
    {
        var program = FixedPriceProgram(price: 45000);
        program.AllowedChannelsMask = 1 << (int)SalesChannel.Counter;

        var result = await BookOneSeatWithProgramsAsync(listPrice: 80000, candidates: new List<PromotionProgram> { program });

        result.FinalAmount.Should().Be(80000);
    }

}

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

    [Fact]
    public async Task GetSeatsAsync_ReturnsAvailableStatus_WhenNotBookedOrLocked()
    {
        var seats = new List<Seat> { new() { Id = SeatId1, RowName = "A", ColIndex = 1 } };
        _uowMock.Setup(u => u.SeatStore.GetByRoomAsync(RoomId1)).ReturnsAsync(seats);
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
        _uowMock.Setup(u => u.SeatStore.GetByRoomAsync(RoomId2)).ReturnsAsync(seats);
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
        _uowMock.Setup(u => u.SeatStore.GetByRoomAsync(RoomId1)).ReturnsAsync(seats);
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatIdsAsync(ShowTimeId1, RoomId1)).ReturnsAsync(new List<Guid>());
        _uowMock.Setup(u => u.ShowTimeStore.GetShowTimeRoomAsync(ShowTimeId1, RoomId1)).ReturnsAsync((ShowTimeRoom?)null);

        var result = await _sut.GetSeatsAsync(SeatSearch(ShowTimeId1, RoomId1));

        result.Results.First().IsDouble.Should().BeTrue();
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
            .ReturnsAsync(new ShowTimeRoom { ShowTimeId = showTimeId, RoomId = roomId, BasePrice = basePrice });
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
            ticketPrices: new List<TicketPrice> { new() { TheaterId = theaterId, RoomTypeId = roomTypeId, TimeSlotId = timeSlotId, IsHoliday = false, PriceMultiplier = 1.2 } },
            categories: new List<PatronCategory> { new() { Id = adultId, TheaterId = theaterId, SeatTypeId = seatTypeId, Name = "Adult", Price = 90000, IsActive = true } },
            kindMap: new Dictionary<SeatKind, Guid> { [SeatKind.Standard] = seatTypeId });
        _uowMock.Setup(u => u.RoomTypePatronCategoryPriceStore.FindByPatronCategoriesAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<RoomTypePatronCategoryPrice> { new() { RoomTypeId = roomTypeId, PatronCategoryId = adultId, Price = 120000 } });

        var result = await _sut.GetShowTimePricesAsync(ShowTimeId1, RoomId1);

        // 120000 x 1.2 + 40000 + 30000 = 214000
        result.Single().Price.Should().Be(214000);
    }

    [Fact]
    public async Task GetShowTimePricesAsync_HolidayFactor_SkippedWhenMatrixRowMatches()
    {
        var theaterId  = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var timeSlotId = Guid.NewGuid();
        var seatTypeId = Guid.NewGuid();
        var adultId    = Guid.NewGuid();

        SetupPricingContext(
            theaterId, roomTypeId, ShowTimeId1, RoomId1, basePrice: 0,
            startTime: new DateTime(2026, 1, 1, 19, 0, 0), projectionForm: ProjectionForm.TwoD,
            holidays: new List<Holiday> { new() { Date = new DateOnly(2026, 1, 1), PriceMultiplier = 1.5 } },
            timeSlots: new List<TimeSlot> { new() { Id = timeSlotId, TheaterId = theaterId, StartTime = "18:00", EndTime = "22:00" } },
            ticketPrices: new List<TicketPrice> { new() { TheaterId = theaterId, RoomTypeId = roomTypeId, TimeSlotId = timeSlotId, IsHoliday = true, PriceMultiplier = 2 } },
            categories: new List<PatronCategory> { new() { Id = adultId, TheaterId = theaterId, SeatTypeId = seatTypeId, Name = "Adult", Price = 100, IsActive = true } },
            kindMap: new Dictionary<SeatKind, Guid> { [SeatKind.Standard] = seatTypeId });

        var result = await _sut.GetShowTimePricesAsync(ShowTimeId1, RoomId1);

        // A matching matrix row is already holiday-scoped: 100 x 2 = 200, NOT 100 x 2 x 1.5.
        result.Single().Price.Should().Be(200);
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
        _uowMock.Setup(u => u.SeatStore.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, Seat> { [seat] = new() { Id = seat, RowName = "H", ColIndex = 1 } });

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
        _uowMock.Setup(u => u.SeatStore.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, Seat> { [seat] = new() { Id = seat, RowName = "I", ColIndex = 1 } });

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
            .ReturnsAsync(new ShowTimeRoom { ShowTimeId = ShowTimeId1, RoomId = RoomId1, BasePrice = 100 });
        _uowMock.Setup(u => u.RoomStore.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Room?)null);
        _uowMock.Setup(u => u.SeatStore.GetBookedSeatIdsAsync(ShowTimeId1, RoomId1)).ReturnsAsync(new List<Guid>());
        _uowMock.Setup(u => u.SeatStore.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, Seat> { [heldSeat] = new() { Id = heldSeat, RowName = "A", ColIndex = 1 } });

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

    // ── Patron category pricing ─────────────────────────────────────────────────

    private void SetupBaselineBookingMocks(Guid theaterId, Guid roomTypeId, Guid showTimeId, Guid roomId, int basePrice)
    {
        _uowMock.Setup(u => u.ShowTimeStore.GetShowTimeRoomAsync(showTimeId, roomId))
            .ReturnsAsync(new ShowTimeRoom { ShowTimeId = showTimeId, RoomId = roomId, BasePrice = basePrice });
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
        _uowMock.Setup(u => u.SeatStore.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, Seat>
            {
                [seatA] = new() { Id = seatA, RowName = "A", ColIndex = 1 },
                [seatB] = new() { Id = seatB, RowName = "A", ColIndex = 2 },
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
    public async Task CreateBookingAsync_RejectsSeatWithNoPatronCategory()
    {
        var theaterId  = Guid.NewGuid();
        var roomTypeId = Guid.NewGuid();
        var seat       = Guid.NewGuid();

        SetupBaselineBookingMocks(theaterId, roomTypeId, ShowTimeId1, RoomId1, 100);
        _uowMock.Setup(u => u.SeatStore.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, Seat> { [seat] = new() { Id = seat, RowName = "B", ColIndex = 1 } });

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
        _uowMock.Setup(u => u.SeatStore.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, Seat> { [seat] = new() { Id = seat, RowName = "C", ColIndex = 1 } });
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
        _uowMock.Setup(u => u.SeatStore.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, Seat> { [seat] = new() { Id = seat, RowName = "D", ColIndex = 1 } });
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
        _uowMock.Setup(u => u.SeatStore.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, Seat> { [seat] = new() { Id = seat, RowName = "E", ColIndex = 1, SeatGroupId = groupId } });
        _uowMock.Setup(u => u.SeatStore.FindAsync(It.IsAny<Expression<Func<Seat, bool>>>()))
            .ReturnsAsync(new List<Seat> { new() { Id = seat, RowName = "E", ColIndex = 1, SeatGroupId = groupId } });

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
        _uowMock.Setup(u => u.SeatStore.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, Seat>
            {
                [seatA] = new() { Id = seatA, RowName = "F", ColIndex = 1, SeatGroupId = groupId },
                [seatB] = new() { Id = seatB, RowName = "F", ColIndex = 2, SeatGroupId = groupId },
            });
        _uowMock.Setup(u => u.SeatStore.FindAsync(It.IsAny<Expression<Func<Seat, bool>>>()))
            .ReturnsAsync(new List<Seat>
            {
                new() { Id = seatA, RowName = "F", ColIndex = 1, SeatGroupId = groupId },
                new() { Id = seatB, RowName = "F", ColIndex = 2, SeatGroupId = groupId },
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
        _uowMock.Setup(u => u.SeatStore.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, Seat> { [seatA] = new() { Id = seatA, RowName = "G", ColIndex = 1, SeatGroupId = groupId } });
        _uowMock.Setup(u => u.SeatStore.FindAsync(It.IsAny<Expression<Func<Seat, bool>>>()))
            .ReturnsAsync(new List<Seat>
            {
                new() { Id = seatA, RowName = "G", ColIndex = 1, SeatGroupId = groupId },
                new() { Id = seatB, RowName = "G", ColIndex = 2, SeatGroupId = groupId },
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
    public async Task RefundBookingAsync_NotPaid_ReturnsFalse()
    {
        var userId  = Guid.NewGuid();
        var invoice = new Invoice { Id = Guid.NewGuid(), UserId = userId, Status = InvoiceStatus.Pending };
        _uowMock.Setup(u => u.InvoiceStore.GetWithDetailsAsync(invoice.Id)).ReturnsAsync(invoice);

        var result = await _sut.RefundBookingAsync(userId, invoice.Id, isAdmin: false);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RefundBookingAsync_WrongUserNonAdmin_ReturnsFalse()
    {
        var invoice = new Invoice { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Status = InvoiceStatus.Paid };
        _uowMock.Setup(u => u.InvoiceStore.GetWithDetailsAsync(invoice.Id)).ReturnsAsync(invoice);

        var result = await _sut.RefundBookingAsync(Guid.NewGuid(), invoice.Id, isAdmin: false);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RefundBookingAsync_ValidPaid_RefundsReversesPointsAndSetsRefunded()
    {
        var userId  = Guid.NewGuid();
        var user    = new User { Id = userId, Email = "u@cinema.vn", Points = 50 };
        var invoice = new Invoice
        {
            Id               = Guid.NewGuid(),
            UserId           = userId,
            Status           = InvoiceStatus.Paid,
            FinalAmount      = 100000,          // originally accrued 10 points (1 / 10,000 VND)
            PaymentMethod    = "Sandbox",
            PaymentReference = "SANDBOX-abc",
            User             = user,
        };
        _uowMock.Setup(u => u.InvoiceStore.GetWithDetailsAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(invoice)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.UserStore.UpdateAsync(user)).ReturnsAsync(user);
        _uowMock.Setup(u => u.MemberShipStore.FindAsync(
                It.IsAny<Expression<Func<MemberShip, bool>>>()))
            .ReturnsAsync(new List<MemberShip>());
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _sut.RefundBookingAsync(userId, invoice.Id, isAdmin: false);

        result.Should().BeTrue();
        invoice.Status.Should().Be(InvoiceStatus.Refunded);
        invoice.RefundedAt.Should().NotBeNull();
        user.Points.Should().Be(40);
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

    // ── Food stock: deduct at booking, restore on cancel / expire / refund ──────

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
        _uowMock.Setup(u => u.SeatStore.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, Seat> { [seat] = new() { Id = seat, RowName = "A", ColIndex = 1 } });

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

    [Fact]
    public async Task RefundBookingAsync_RestocksNetSoldQuantity()
    {
        var userId  = Guid.NewGuid();
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(), UserId = userId, Status = InvoiceStatus.Paid, FinalAmount = 100000,
            PaymentMethod = "Sandbox", PaymentReference = "SANDBOX-abc", User = new User { Id = userId, Email = "u@cinema.vn", Points = 50 },
        };
        var cola = Food("Cola", 10, tracked: true);
        _uowMock.Setup(u => u.InvoiceStore.GetWithDetailsAsync(invoice.Id)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.InvoiceStore.UpdateAsync(invoice)).ReturnsAsync(invoice);
        _uowMock.Setup(u => u.UserStore.UpdateAsync(It.IsAny<User>())).ReturnsAsync((User x) => x);
        _uowMock.Setup(u => u.MemberShipStore.FindAsync(It.IsAny<Expression<Func<MemberShip, bool>>>())).ReturnsAsync(new List<MemberShip>());
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _uowMock.Setup(u => u.StockMovementStore.GetNetSaleQuantitiesAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<StockNetQuantity> { new(invoice.Id, cola.Id, -4) });
        _uowMock.Setup(u => u.FoodAndDrinkStore.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, FoodAndDrink> { [cola.Id] = cola });
        _uowMock.Setup(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(It.IsAny<Guid>(), It.IsAny<int>())).ReturnsAsync(true);
        var movements = CaptureMovements();

        (await _sut.RefundBookingAsync(userId, invoice.Id, isAdmin: false)).Should().BeTrue();

        _uowMock.Verify(u => u.FoodAndDrinkStore.TryApplyStockDeltaAsync(cola.Id, 4), Times.Once);
        movements.Should().ContainSingle().Which.Reason.Should().Be("Refunded");
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
    }
}

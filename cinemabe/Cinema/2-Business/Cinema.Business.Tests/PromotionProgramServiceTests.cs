using System.Linq.Expressions;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class PromotionProgramServiceTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly PromotionProgramManager _sut;

    private static readonly Guid MemberShipId = Guid.NewGuid();
    private static readonly DateTime Start = new(2026, 1, 1);
    private static readonly DateTime End = new(2026, 12, 31);

    public PromotionProgramServiceTests()
    {
        _sut = new PromotionProgramManager(_uowMock.Object);

        _uowMock.Setup(u => u.PromotionProgramStore.GetPricesAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<PromotionProgramPrice>());
        _uowMock.Setup(u => u.PromotionProgramStore.GetSchedulesAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<PromotionProgramSchedule>());
        _uowMock.Setup(u => u.MemberShipStore.ExistsAsync(It.IsAny<Expression<Func<MemberShip, bool>>>()))
            .ReturnsAsync(true);
        _uowMock.Setup(u => u.DiscountStore.ExistsAsync(It.IsAny<Expression<Func<Discount, bool>>>()))
            .ReturnsAsync(true);
        _uowMock.Setup(u => u.PromotionProgramStore.CreateAsync(It.IsAny<PromotionProgram>()))
            .ReturnsAsync((PromotionProgram p) => p);
    }

    private static CreatePromotionProgramRequest BaseRequest() => new()
    {
        Title = "Happy Hour",
        Mechanic = PromotionMechanic.FixedPrice,
        Eligibility = PromotionEligibility.Everyone,
        StartDate = Start,
        EndDate = End,
        Prices = new List<PromotionProgramPriceDTO> { new() { Price = 45000 } },
    };

    [Fact]
    public async Task CreateAsync_AutoApplyInfo_Rejected()
    {
        var request = BaseRequest();
        request.Mechanic = PromotionMechanic.Info;
        request.Prices = new();
        request.AutoApply = true;

        var act = () => _sut.CreateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_AutoApplyStudentOrTeacher_Rejected()
    {
        var request = BaseRequest();
        request.Eligibility = PromotionEligibility.StudentOrTeacher;
        request.AutoApply = true;

        var act = () => _sut.CreateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_AutoApplyWithLinkedDiscount_Rejected()
    {
        var request = BaseRequest();
        request.AutoApply = true;
        request.LinkedDiscountId = Guid.NewGuid();

        var act = () => _sut.CreateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_FixedPriceWithNoPriceRows_Rejected()
    {
        var request = BaseRequest();
        request.Prices = new();

        var act = () => _sut.CreateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_PercentOffOutOfRange_Rejected()
    {
        var request = BaseRequest();
        request.Mechanic = PromotionMechanic.PercentOff;
        request.Prices = new();
        request.PercentOff = 150;

        var act = () => _sut.CreateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_MembersWithNoTier_Rejected()
    {
        var request = BaseRequest();
        request.Eligibility = PromotionEligibility.Members;

        var act = () => _sut.CreateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_AgeLimitWithNoMaxAge_Rejected()
    {
        var request = BaseRequest();
        request.Eligibility = PromotionEligibility.AgeLimit;

        var act = () => _sut.CreateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_EndBeforeStart_Rejected()
    {
        var request = BaseRequest();
        request.EndDate = request.StartDate.AddDays(-1);

        var act = () => _sut.CreateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_ScheduleWithOnlyStartTime_Rejected()
    {
        var request = BaseRequest();
        request.Schedules = new List<PromotionProgramScheduleDTO> { new() { StartTimeOfDay = "08:00" } };

        var act = () => _sut.CreateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_DuplicatePriceRows_Rejected()
    {
        var request = BaseRequest();
        request.Prices = new List<PromotionProgramPriceDTO>
        {
            new() { ProjectionForm = ProjectionForm.TwoD, Price = 45000 },
            new() { ProjectionForm = ProjectionForm.TwoD, Price = 50000 },
        };

        var act = () => _sut.CreateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_ValidEveryoneAutoApplyProgram_Saved()
    {
        var request = BaseRequest();
        request.AutoApply = true;

        var dto = await _sut.CreateAsync(request);

        dto.Title.Should().Be("Happy Hour");
        dto.AutoApply.Should().BeTrue();
        dto.Prices.Should().ContainSingle(p => p.Price == 45000);
    }

    [Fact]
    public async Task CreateAsync_ValidMembersProgram_Saved()
    {
        var request = BaseRequest();
        request.Eligibility = PromotionEligibility.Members;
        request.MinMemberShipId = MemberShipId;

        var dto = await _sut.CreateAsync(request);

        dto.Eligibility.Should().Be(PromotionEligibility.Members);
        dto.MinMemberShipId.Should().Be(MemberShipId);
    }

    [Fact]
    public async Task CreateAsync_InfoMechanic_ClearsPriceRows()
    {
        var request = BaseRequest();
        request.Mechanic = PromotionMechanic.Info;
        request.Prices = new List<PromotionProgramPriceDTO> { new() { Price = 45000 } };

        var dto = await _sut.CreateAsync(request);

        dto.Prices.Should().BeEmpty();
    }
}

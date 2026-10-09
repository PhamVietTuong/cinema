using Cinema.Business.Contracts.Exceptions;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.Staff;
using Cinema.Business.Managers;
using Cinema.Business.Security;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class AuditOverrideTests
{
    private const string _pin = "4321";

    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly List<AuditLog> _staged = new();
    private readonly ManagerOverrideService _sut;
    private readonly Guid _theaterId = Guid.NewGuid();
    private readonly User _staff;
    private readonly User _manager;

    public AuditOverrideTests()
    {
        _uowMock.Setup(u => u.AuditLogStore.Stage(It.IsAny<AuditLog>()))
            .Callback<AuditLog>(row => _staged.Add(row));
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _uowMock.Setup(u => u.UserStore.UpdateAsync(It.IsAny<User>())).ReturnsAsync((User user) => user);

        _staff = NewUser(RoleNames.BoxOfficeStaff, _theaterId);
        _manager = NewUser(RoleNames.TheaterManager, _theaterId);
        PasswordHasher.CreateHash(_pin, out var hash, out var salt);
        _manager.OverridePinHash = hash;
        _manager.OverridePinSalt = salt;

        _sut = new ManagerOverrideService(_uowMock.Object, new AuditLogger(_uowMock.Object));
    }

    private User NewUser(string role, Guid? theaterId)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = role,
            Status = UserStatus.Active,
            TheaterId = theaterId,
            UserType = new UserType { Id = Guid.NewGuid(), Name = role }
        };
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(user.Id)).ReturnsAsync(user);
        return user;
    }

    private ManagerOverrideDTO Override(User approver, string pin) => new() { ApproverUserId = approver.Id, Pin = pin };

    [Fact]
    public async Task Verify_CorrectPin_ReturnsApproverId_AndWritesNoFailureAudit()
    {
        var approverId = await _sut.VerifyAsync(_theaterId, _staff.Id, Override(_manager, _pin), AuditAction.Refund);

        approverId.Should().Be(_manager.Id);
        _staged.Should().BeEmpty();
    }

    [Fact]
    public async Task Verify_CorrectPin_ResetsFailedCounter()
    {
        _manager.OverridePinFailedCount = 3;

        await _sut.VerifyAsync(_theaterId, _staff.Id, Override(_manager, _pin), AuditAction.Refund);

        _manager.OverridePinFailedCount.Should().Be(0);
    }

    [Fact]
    public async Task Verify_WrongPin_Throws403Exception_IncrementsCounter_AndAuditsOverrideFailed()
    {
        var act = () => _sut.VerifyAsync(_theaterId, _staff.Id, Override(_manager, "0000"), AuditAction.Refund);

        await act.Should().ThrowAsync<AccessDeniedException>();
        _manager.OverridePinFailedCount.Should().Be(1);
        _manager.OverridePinLockoutEndUtc.Should().BeNull();
        _staged.Should().ContainSingle();
        _staged[0].Action.Should().Be(AuditAction.OverrideFailed);
        _staged[0].ActorUserId.Should().Be(_staff.Id);
        _staged[0].ApproverUserId.Should().Be(_manager.Id);
        _staged[0].TheaterId.Should().Be(_theaterId);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Verify_FiveWrongPins_LocksOut_SoEvenTheCorrectPinIsRefused()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var wrong = () => _sut.VerifyAsync(_theaterId, _staff.Id, Override(_manager, "0000"), AuditAction.Refund);
            await wrong.Should().ThrowAsync<AccessDeniedException>();
        }

        _manager.OverridePinFailedCount.Should().Be(5);
        _manager.OverridePinLockoutEndUtc.Should().BeAfter(DateTime.UtcNow);

        var correct = () => _sut.VerifyAsync(_theaterId, _staff.Id, Override(_manager, _pin), AuditAction.Refund);
        await correct.Should().ThrowAsync<AccessDeniedException>();
        _staged.Should().HaveCount(6).And.OnlyContain(r => r.Action == AuditAction.OverrideFailed);
    }

    [Fact]
    public async Task Verify_AfterLockoutExpires_CorrectPinWorksAgain()
    {
        _manager.OverridePinFailedCount = 5;
        _manager.OverridePinLockoutEndUtc = DateTime.UtcNow.AddMinutes(-1);

        var approverId = await _sut.VerifyAsync(_theaterId, _staff.Id, Override(_manager, _pin), AuditAction.Refund);

        approverId.Should().Be(_manager.Id);
        _manager.OverridePinFailedCount.Should().Be(0);
        _manager.OverridePinLockoutEndUtc.Should().BeNull();
    }

    [Fact]
    public async Task Verify_ApproverFromAnotherTheater_IsRefused_AndAudited()
    {
        var otherTheaterManager = NewUser(RoleNames.TheaterManager, Guid.NewGuid());
        PasswordHasher.CreateHash(_pin, out var hash, out var salt);
        otherTheaterManager.OverridePinHash = hash;
        otherTheaterManager.OverridePinSalt = salt;

        var act = () => _sut.VerifyAsync(_theaterId, _staff.Id, Override(otherTheaterManager, _pin), AuditAction.Refund);

        await act.Should().ThrowAsync<AccessDeniedException>();
        _staged.Should().ContainSingle(r => r.Action == AuditAction.OverrideFailed);
    }

    [Fact]
    public async Task Verify_ApproverWhoIsNotAnApproverRole_IsRefused()
    {
        var colleague = NewUser(RoleNames.TheaterStaff, _theaterId);
        PasswordHasher.CreateHash(_pin, out var hash, out var salt);
        colleague.OverridePinHash = hash;
        colleague.OverridePinSalt = salt;

        var act = () => _sut.VerifyAsync(_theaterId, _staff.Id, Override(colleague, _pin), AuditAction.Refund);

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    public async Task Verify_NoOverrideSupplied_ForNonApprover_Throws403Exception()
    {
        var act = () => _sut.VerifyAsync(_theaterId, _staff.Id, null, AuditAction.Refund);

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    public async Task Verify_ActorIsAManagerOfTheTheater_NeedsNoPin()
    {
        var approverId = await _sut.VerifyAsync(_theaterId, _manager.Id, null, AuditAction.Refund);

        approverId.Should().Be(_manager.Id);
        _staged.Should().BeEmpty();
    }

    [Fact]
    public async Task Verify_ActorIsAdmin_NeedsNoPin()
    {
        var admin = NewUser(RoleNames.Admin, null);

        var approverId = await _sut.VerifyAsync(_theaterId, admin.Id, null, AuditAction.PriceOverride);

        approverId.Should().Be(admin.Id);
    }

    [Fact]
    public async Task Verify_ActorIsManagerOfAnotherTheater_StillNeedsAPin()
    {
        var foreignManager = NewUser(RoleNames.TheaterManager, Guid.NewGuid());

        var act = () => _sut.VerifyAsync(_theaterId, foreignManager.Id, null, AuditAction.Refund);

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("12345678")]
    public async Task SetPin_ValidPin_StoresSaltedHash_ClearsLockout_AndAuditsChange(string pin)
    {
        _manager.OverridePinFailedCount = 5;
        _manager.OverridePinLockoutEndUtc = DateTime.UtcNow.AddMinutes(10);

        await _sut.SetPinAsync(_manager.Id, pin);

        _manager.OverridePinHash.Should().NotBeNull();
        _manager.OverridePinSalt.Should().NotBeNull();
        PasswordHasher.Verify(pin, _manager.OverridePinHash!, _manager.OverridePinSalt!).Should().BeTrue();
        _manager.OverridePinFailedCount.Should().Be(0);
        _manager.OverridePinLockoutEndUtc.Should().BeNull();
        _staged.Should().ContainSingle(r => r.Action == AuditAction.OverridePinChanged && r.ActorUserId == _manager.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("123456789")]
    [InlineData("12a4")]
    public async Task SetPin_InvalidPin_Throws(string pin)
    {
        var act = () => _sut.SetPinAsync(_manager.Id, pin);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AuditLogger_OnlyStages_NeverSaves()
    {
        var logger = new AuditLogger(_uowMock.Object);

        await logger.LogAsync(new AuditEntry { ActorUserId = _staff.Id, Action = AuditAction.Refund, EntityType = nameof(Invoice) });

        _staged.Should().ContainSingle();
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }
}

using System.Linq.Expressions;
using Cinema.Business.Contracts;
using Cinema.Business.Contracts.Auth;
using Cinema.Business.DTO.Auth;
using Cinema.Business.Managers.Auth;
using Cinema.Business.Notifications;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class AuthUserTheaterTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly AuthManager _sut;
    private readonly Guid _theaterId = Guid.NewGuid();
    private User? _created;

    public AuthUserTheaterTests()
    {
        _sut = new AuthManager(
            _uowMock.Object,
            new Mock<ITokenService>().Object,
            new DevLogNotificationService(),
            new DevLogSmsNotificationService(),
            new Mock<IGoogleTokenValidator>().Object,
            new Mock<IFacebookTokenValidator>().Object);

        _uowMock.Setup(u => u.UserStore.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        _uowMock.Setup(u => u.UserStore.GetByPhoneAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        _uowMock.Setup(u => u.UserStore.CreateAsync(It.IsAny<User>()))
            .Callback<User>(user => _created = user)
            .ReturnsAsync((User user) => user);
        _uowMock.Setup(u => u.TheaterStore.ExistsAsync(It.IsAny<Expression<Func<Theater, bool>>>()))
            .ReturnsAsync((Expression<Func<Theater, bool>> predicate) =>
                predicate.Compile()(new Theater { Id = _theaterId }));
    }

    private Guid SetupRole(string roleName)
    {
        var typeId = Guid.NewGuid();
        _uowMock.Setup(u => u.UserTypeStore.GetByIdAsync(typeId))
            .ReturnsAsync(new UserType { Id = typeId, Name = roleName });
        return typeId;
    }

    private static CreateUserRequest NewCreate(Guid typeId, Guid? theaterId) => new()
    {
        Name = "Staff",
        Email = "staff@test.com",
        Phone = "0911111111",
        Password = "Password@1",
        UserTypeId = typeId,
        TheaterId = theaterId,
    };

    [Theory]
    [InlineData(RoleNames.TheaterStaff)]
    [InlineData(RoleNames.TheaterManager)]
    public async Task CreateUser_TheaterRoleWithoutTheater_Throws(string role)
    {
        var typeId = SetupRole(role);

        var act = () => _sut.CreateUserAsync(NewCreate(typeId, null));

        await act.Should().ThrowAsync<InvalidOperationException>();
        _created.Should().BeNull();
    }

    [Theory]
    [InlineData(RoleNames.TheaterStaff)]
    [InlineData(RoleNames.TheaterManager)]
    public async Task CreateUser_TheaterRoleWithUnknownTheater_Throws(string role)
    {
        var typeId = SetupRole(role);

        var act = () => _sut.CreateUserAsync(NewCreate(typeId, Guid.NewGuid()));

        await act.Should().ThrowAsync<InvalidOperationException>();
        _created.Should().BeNull();
    }

    [Theory]
    [InlineData(RoleNames.TheaterStaff)]
    [InlineData(RoleNames.TheaterManager)]
    public async Task CreateUser_TheaterRoleWithValidTheater_KeepsTheater(string role)
    {
        var typeId = SetupRole(role);

        await _sut.CreateUserAsync(NewCreate(typeId, _theaterId));

        _created.Should().NotBeNull();
        _created!.TheaterId.Should().Be(_theaterId);
    }

    [Theory]
    [InlineData(RoleNames.Customer)]
    [InlineData(RoleNames.Admin)]
    public async Task CreateUser_NonTheaterRole_ForcesTheaterNull(string role)
    {
        var typeId = SetupRole(role);

        await _sut.CreateUserAsync(NewCreate(typeId, _theaterId));

        _created.Should().NotBeNull();
        _created!.TheaterId.Should().BeNull();
    }

    private User ExistingUser(Guid typeId, Guid? theaterId)
    {
        var user = new User { Id = Guid.NewGuid(), Name = "Old", UserTypeId = typeId, TheaterId = theaterId };
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(user.Id)).ReturnsAsync(user);
        return user;
    }

    private static UpdateUserRequest NewUpdate(Guid id, Guid typeId, Guid? theaterId) => new()
    {
        Id = id,
        Name = "Updated",
        Phone = "0922222222",
        UserTypeId = typeId,
        TheaterId = theaterId,
    };

    [Theory]
    [InlineData(RoleNames.TheaterStaff)]
    [InlineData(RoleNames.TheaterManager)]
    public async Task UpdateUser_TheaterRoleWithoutTheater_Throws(string role)
    {
        var typeId = SetupRole(role);
        var user = ExistingUser(typeId, _theaterId);

        var act = () => _sut.UpdateUserAsync(NewUpdate(user.Id, typeId, null));

        await act.Should().ThrowAsync<InvalidOperationException>();
        user.TheaterId.Should().Be(_theaterId);
    }

    [Theory]
    [InlineData(RoleNames.TheaterStaff)]
    [InlineData(RoleNames.TheaterManager)]
    public async Task UpdateUser_TheaterRoleWithUnknownTheater_Throws(string role)
    {
        var typeId = SetupRole(role);
        var user = ExistingUser(typeId, _theaterId);

        var act = () => _sut.UpdateUserAsync(NewUpdate(user.Id, typeId, Guid.NewGuid()));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData(RoleNames.Customer)]
    [InlineData(RoleNames.Admin)]
    public async Task UpdateUser_NonTheaterRole_ForcesTheaterNull(string role)
    {
        var typeId = SetupRole(role);
        var user = ExistingUser(typeId, _theaterId);

        await _sut.UpdateUserAsync(NewUpdate(user.Id, typeId, _theaterId));

        user.TheaterId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateUser_NoUserTypeSupplied_UsesCurrentRoleForTheaterRule()
    {
        var typeId = SetupRole(RoleNames.TheaterStaff);
        var user = ExistingUser(typeId, _theaterId);

        var act = () => _sut.UpdateUserAsync(NewUpdate(user.Id, Guid.Empty, null));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}

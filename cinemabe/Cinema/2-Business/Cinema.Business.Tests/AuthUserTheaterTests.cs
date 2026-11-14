using System.Linq.Expressions;
using Cinema.Business.Contracts;
using Cinema.Business.Contracts.Auth;
using Cinema.Business.DTO;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.Requests;
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
        _uowMock.Setup(u => u.AuditLogStore.Stage(It.IsAny<AuditLog>()));
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
    public async Task CreateUser_TheaterRoleWithoutTheater_Throws(string role)
    {
        var typeId = SetupRole(role);

        var act = () => _sut.CreateUserAsync(NewCreate(typeId, null));

        await act.Should().ThrowAsync<InvalidOperationException>();
        _created.Should().BeNull();
    }

    [Theory]
    [InlineData(RoleNames.TheaterStaff)]
    public async Task CreateUser_TheaterRoleWithUnknownTheater_Throws(string role)
    {
        var typeId = SetupRole(role);

        var act = () => _sut.CreateUserAsync(NewCreate(typeId, Guid.NewGuid()));

        await act.Should().ThrowAsync<InvalidOperationException>();
        _created.Should().BeNull();
    }

    [Theory]
    [InlineData(RoleNames.TheaterStaff)]
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

    [Fact]
    public async Task GetUsersAsync_MixedTheaterAndNoTheaterUsers_ResolvesNamesCorrectly()
    {
        var staffUser = new User { Id = Guid.NewGuid(), Name = "Staff", TheaterId = _theaterId };
        var customerUser = new User { Id = Guid.NewGuid(), Name = "Customer", TheaterId = null };
        _uowMock.Setup(u => u.UserStore.GetPagedAsync(null, 1, 20, null))
            .ReturnsAsync((new[] { staffUser, customerUser }, 2));
        _uowMock.Setup(u => u.TheaterStore.GetNamesByIdsAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(_theaterId) && ids.Count == 1)))
            .ReturnsAsync(new Dictionary<Guid, string> { [_theaterId] = "Cinema A" });

        var result = await _sut.GetUsersAsync(new PagingSearchDTO());

        var dtos = result.Results.ToList();
        dtos.Single(d => d.Id == staffUser.Id).TheaterName.Should().Be("Cinema A");
        dtos.Single(d => d.Id == customerUser.Id).TheaterName.Should().BeNull();
    }

    [Fact]
    public async Task GetUsersAsync_TheaterIdFilter_IsPassedThroughToStore()
    {
        _uowMock.Setup(u => u.UserStore.GetPagedAsync(null, 1, 20, _theaterId))
            .ReturnsAsync((Array.Empty<User>(), 0));
        _uowMock.Setup(u => u.TheaterStore.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        var search = new PagingSearchDTO { Filters = new Dictionary<string, string> { ["theaterId"] = _theaterId.ToString() } };
        var result = await _sut.GetUsersAsync(search);

        result.TotalCount.Should().Be(0);
        _uowMock.Verify(u => u.UserStore.GetPagedAsync(null, 1, 20, _theaterId), Times.Once);
    }

    [Fact]
    public async Task GetUsersAsync_StaleTheaterId_ResolvesNullWithoutThrowing()
    {
        var staffUser = new User { Id = Guid.NewGuid(), Name = "Staff", TheaterId = _theaterId };
        _uowMock.Setup(u => u.UserStore.GetPagedAsync(null, 1, 20, null))
            .ReturnsAsync((new[] { staffUser }, 1));
        // The theater row no longer exists (e.g. SetNull race) -- lookup dictionary misses it.
        _uowMock.Setup(u => u.TheaterStore.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        var act = () => _sut.GetUsersAsync(new PagingSearchDTO());

        var result = await act.Should().NotThrowAsync();
        result.Subject.Results.Single().TheaterName.Should().BeNull();
    }
}

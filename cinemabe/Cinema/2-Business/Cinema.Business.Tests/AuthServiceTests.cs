using System.Linq.Expressions;
using Cinema.Business.Contracts;
using Cinema.Business.DTO.Auth;
using Cinema.Business.Managers;
using Cinema.Business.Notifications;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class AuthServiceTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly Mock<ITokenService> _tokenMock = new();
    private readonly AuthManager _sut;

    public AuthServiceTests()
    {
        _sut = new AuthManager(_uowMock.Object, _tokenMock.Object, new DevLogNotificationService());
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsAuthResponse()
    {
        using var hmac = new System.Security.Cryptography.HMACSHA512();
        var passwordBytes = System.Text.Encoding.UTF8.GetBytes("Password@1");
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            Name = "Test User",
            PasswordSalt = hmac.Key,
            PasswordHash = hmac.ComputeHash(passwordBytes),
            UserTypeId = Guid.NewGuid(),
            UserType = new UserType { Name = "User" }
        };

        _uowMock.Setup(u => u.UserStore.GetByEmailAsync("test@test.com")).ReturnsAsync(user);
        _tokenMock.Setup(t => t.GenerateToken(user)).Returns("jwt-token");
        _tokenMock.Setup(t => t.GetTokenExpiry()).Returns(DateTime.UtcNow.AddHours(24));

        var result = await _sut.LoginAsync(new LoginRequest { EmailOrPhone = "test@test.com", Password = "Password@1" });

        result.Should().NotBeNull();
        result.Token.Should().Be("jwt-token");
    }

    [Fact]
    public async Task Login_InvalidPassword_ThrowsUnauthorized()
    {
        using var hmac = new System.Security.Cryptography.HMACSHA512();
        var user = new User
        {
            Email = "test@test.com",
            PasswordSalt = hmac.Key,
            PasswordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes("CorrectPassword@1"))
        };

        _uowMock.Setup(u => u.UserStore.GetByEmailAsync("test@test.com")).ReturnsAsync(user);
        _uowMock.Setup(u => u.UserStore.GetByPhoneAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        await _sut.Invoking(s => s.LoginAsync(new LoginRequest { EmailOrPhone = "test@test.com", Password = "WrongPassword@1" }))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Login_UserNotFound_ThrowsUnauthorized()
    {
        _uowMock.Setup(u => u.UserStore.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        _uowMock.Setup(u => u.UserStore.GetByPhoneAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        await _sut.Invoking(s => s.LoginAsync(new LoginRequest { EmailOrPhone = "nobody@test.com", Password = "Pass@1" }))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Register_NewEmail_CreatesUserAndReturnsToken()
    {
        var createdUser = new User { Email = "new@test.com", Name = "New User", UserType = new UserType { Name = "User" } };
        _uowMock.Setup(u => u.UserStore.GetByEmailAsync("new@test.com")).ReturnsAsync((User?)null);
        _uowMock.Setup(u => u.UserStore.GetByPhoneAsync("0900000000")).ReturnsAsync((User?)null);
        _uowMock.Setup(u => u.UserStore.CreateAsync(It.IsAny<User>())).ReturnsAsync(createdUser);
        _uowMock.Setup(u => u.UserTypeStore.FindSingleAsync(It.IsAny<Expression<Func<UserType, bool>>>()))
            .ReturnsAsync(new UserType { Id = Guid.NewGuid(), Name = "Customer" });
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _tokenMock.Setup(t => t.GenerateToken(It.IsAny<User>())).Returns("jwt-token");
        _tokenMock.Setup(t => t.GetTokenExpiry()).Returns(DateTime.UtcNow.AddHours(24));

        var result = await _sut.RegisterAsync(new RegisterRequest
        {
            Email = "new@test.com",
            Password = "Password@1",
            Name = "New User",
            Phone = "0900000000"
        });

        result.Should().NotBeNull();
        result.Token.Should().Be("jwt-token");
        _uowMock.Verify(u => u.UserStore.CreateAsync(It.IsAny<User>()), Times.Once);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ThrowsInvalidOperation()
    {
        _uowMock.Setup(u => u.UserStore.GetByEmailAsync("dup@test.com"))
            .ReturnsAsync(new User { Email = "dup@test.com" });

        await _sut.Invoking(s => s.RegisterAsync(new RegisterRequest
        {
            Email = "dup@test.com",
            Password = "Password@1",
            Name = "User",
            Phone = "0900000000"
        })).Should().ThrowAsync<InvalidOperationException>()
           .WithMessage("*Email*");
    }

    [Fact]
    public async Task GetProfile_ExistingUser_ReturnsMappedDto()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "u@test.com", Name = "User", UserType = new UserType { Name = "User" } };
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(userId)).ReturnsAsync(user);

        var result = await _sut.GetProfileAsync(userId);

        result.Should().NotBeNull();
        result.Id.Should().Be(userId);
    }

    [Fact]
    public async Task GetProfile_NotFound_ThrowsKeyNotFound()
    {
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

        await _sut.Invoking(s => s.GetProfileAsync(Guid.NewGuid()))
            .Should().ThrowAsync<KeyNotFoundException>();
    }
}

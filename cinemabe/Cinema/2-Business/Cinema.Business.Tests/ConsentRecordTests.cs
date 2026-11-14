using System.Linq.Expressions;
using System.Text.Json;
using Cinema.Business.Contracts;
using Cinema.Business.Contracts.Auth;
using Cinema.Business.DTO.Auth;
using Cinema.Business.Managers.Auth;
using Cinema.Business.Notifications;
using Cinema.Data.Contexts;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Cinema.Business.Tests;

/// <summary>Promotional-email consent: opt-in by default, and every change is written to the AuditLog.</summary>
public class ConsentRecordTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly Mock<ITokenService> _tokenMock = new();
    private readonly Mock<IGoogleTokenValidator> _googleMock = new();
    private readonly Mock<IFacebookTokenValidator> _facebookMock = new();
    private readonly List<AuditLog> _audits = new();
    // Call order the persistence relies on: UserStore.CreateAsync saves internally, so the row must be staged first.
    private readonly List<string> _order = new();
    private readonly AuthManager _sut;

    public ConsentRecordTests()
    {
        _sut = new AuthManager(_uowMock.Object, _tokenMock.Object, new DevLogNotificationService(), new DevLogSmsNotificationService(), _googleMock.Object, _facebookMock.Object);
        _uowMock.Setup(u => u.AuditLogStore.Stage(It.IsAny<AuditLog>())).Callback<AuditLog>(a => { _audits.Add(a); _order.Add($"stage:{a.Action}"); });
        _uowMock.Setup(u => u.UserTypeStore.FindSingleAsync(It.IsAny<Expression<Func<UserType, bool>>>()))
            .ReturnsAsync(new UserType { Id = Guid.NewGuid(), Name = "Customer" });
        _uowMock.Setup(u => u.UserStore.CreateAsync(It.IsAny<User>())).Callback<User>(_ => _order.Add("create")).ReturnsAsync((User u) => u);
        _tokenMock.Setup(t => t.GenerateToken(It.IsAny<User>())).Returns("jwt");
        _tokenMock.Setup(t => t.GetTokenExpiry()).Returns(DateTime.UtcNow.AddHours(1));
    }

    private static JsonElement Data(AuditLog row)
    {
        return JsonDocument.Parse(row.DataJson!).RootElement;
    }

    [Fact]
    public void NewUser_PromotionalEmailDefaultsToOff_OtherPreferencesStayOn()
    {
        var user = new User();

        user.NotifyPromotionEmails.Should().BeFalse();
        user.NotifyBookingEmails.Should().BeTrue();
        user.NotifyReminderEmails.Should().BeTrue();
    }

    [Fact]
    public void EfModel_DoesNotConfigureADatabaseDefault_SoTheEntityValueIsInserted()
    {
        var options = new DbContextOptionsBuilder<CinemaContext>()
            .UseSqlServer("Server=.;Database=ModelMetadataOnly;TrustServerCertificate=True")
            .Options;
        using var context = new CinemaContext(options);

        var property = context.Model.FindEntityType(typeof(User))!.FindProperty(nameof(User.NotifyPromotionEmails))!;

        property.FindAnnotation("Relational:DefaultValue").Should().BeNull();
        property.GetDefaultValueSql().Should().BeNull();
        property.ValueGenerated.Should().Be(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never);
    }

    [Fact]
    public async Task Register_StoresPromotionOffAndOneRegistrationConsentRow()
    {
        _uowMock.Setup(u => u.UserStore.GetByEmailAsync("n@t.com")).ReturnsAsync((User?)null);
        _uowMock.Setup(u => u.UserStore.GetByPhoneAsync("0900000001")).ReturnsAsync((User?)null);
        User? created = null;
        _uowMock.Setup(u => u.UserStore.CreateAsync(It.IsAny<User>())).Callback<User>(u => { created = u; _order.Add("create"); }).ReturnsAsync((User u) => u);

        await _sut.RegisterAsync(new RegisterRequest { Email = "n@t.com", Password = "Password@1", Name = "N", Phone = "0900000001" });

        created!.NotifyPromotionEmails.Should().BeFalse();
        var row = _audits.Should().ContainSingle().Subject;
        row.Action.Should().Be(AuditAction.ConsentChanged);
        row.ActorUserId.Should().Be(created.Id);
        row.EntityId.Should().Be(created.Id);
        row.EntityType.Should().Be(nameof(User));
        Data(row).GetProperty("Channel").GetString().Should().Be("PromotionEmail");
        Data(row).GetProperty("Value").GetBoolean().Should().BeFalse();
        Data(row).GetProperty("Source").GetString().Should().Be("registration");
        _order.Should().Equal("stage:ConsentChanged", "create");
    }

    [Fact]
    public async Task GoogleSignUp_WritesOneRegistrationConsentRow()
    {
        _googleMock.Setup(g => g.ValidateAsync("t")).ReturnsAsync(new GoogleUserInfo { Email = "g@g.com", Name = "G", EmailVerified = true, Subject = "s" });
        _uowMock.Setup(u => u.UserStore.GetByEmailAsync("g@g.com")).ReturnsAsync((User?)null);

        await _sut.LoginWithGoogleAsync(new GoogleLoginRequest { IdToken = "t" });

        var row = _audits.Should().ContainSingle().Subject;
        row.Action.Should().Be(AuditAction.ConsentChanged);
        Data(row).GetProperty("Source").GetString().Should().Be("registration");
        Data(row).GetProperty("Value").GetBoolean().Should().BeFalse();
        _order.Should().Equal("stage:ConsentChanged", "create");
    }

    [Fact]
    public async Task GoogleLogin_ExistingUser_WritesNoConsentRow()
    {
        var existing = new User { Email = "g@g.com", EmailConfirmed = true, UserType = new UserType { Name = "Customer" } };
        _googleMock.Setup(g => g.ValidateAsync("t")).ReturnsAsync(new GoogleUserInfo { Email = "g@g.com", Name = "G", EmailVerified = true, Subject = "s" });
        _uowMock.Setup(u => u.UserStore.GetByEmailAsync("g@g.com")).ReturnsAsync(existing);

        await _sut.LoginWithGoogleAsync(new GoogleLoginRequest { IdToken = "t" });

        _audits.Should().BeEmpty();
    }

    [Fact]
    public async Task FacebookSignUp_WritesOneRegistrationConsentRow()
    {
        _facebookMock.Setup(f => f.ValidateAsync("t")).ReturnsAsync(new FacebookUserInfo { Email = "f@f.com", Name = "F" });
        _uowMock.Setup(u => u.UserStore.GetByEmailAsync("f@f.com")).ReturnsAsync((User?)null);

        await _sut.LoginWithFacebookAsync(new FacebookLoginRequest { AccessToken = "t" });

        var row = _audits.Should().ContainSingle().Subject;
        row.Action.Should().Be(AuditAction.ConsentChanged);
        Data(row).GetProperty("Source").GetString().Should().Be("registration");
        _order.Should().Equal("stage:ConsentChanged", "create");
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task UpdatePreferences_ChangingPromotion_WritesOneProfileRow(bool before, bool after)
    {
        var user = new User { NotifyPromotionEmails = before };
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(user.Id)).ReturnsAsync(user);

        await _sut.UpdateNotificationPreferencesAsync(user.Id, new UpdateNotificationPreferencesRequest
        {
            NotifyBookingEmails = true, NotifyPromotionEmails = after, NotifyReminderEmails = true
        });

        user.NotifyPromotionEmails.Should().Be(after);
        var row = _audits.Should().ContainSingle().Subject;
        row.Action.Should().Be(AuditAction.ConsentChanged);
        row.ActorUserId.Should().Be(user.Id);
        Data(row).GetProperty("Value").GetBoolean().Should().Be(after);
        Data(row).GetProperty("Source").GetString().Should().Be("profile");
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdatePreferences_PromotionUnchanged_WritesNoRow()
    {
        var user = new User { NotifyPromotionEmails = true };
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(user.Id)).ReturnsAsync(user);

        await _sut.UpdateNotificationPreferencesAsync(user.Id, new UpdateNotificationPreferencesRequest
        {
            NotifyBookingEmails = false, NotifyPromotionEmails = true, NotifyReminderEmails = false
        });

        _audits.Should().BeEmpty();
        user.NotifyBookingEmails.Should().BeFalse();
    }

    [Fact]
    public async Task CreateUser_ByAdmin_WritesNoConsentRow()
    {
        var typeId = Guid.NewGuid();
        _uowMock.Setup(u => u.UserTypeStore.FindSingleAsync(It.IsAny<Expression<Func<UserType, bool>>>()))
            .ReturnsAsync(new UserType { Id = typeId, Name = "Customer" });
        _uowMock.Setup(u => u.UserTypeStore.GetByIdAsync(typeId)).ReturnsAsync(new UserType { Id = typeId, Name = "Customer" });
        _uowMock.Setup(u => u.UserStore.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

        await _sut.CreateUserAsync(new CreateUserRequest { Name = "S", Email = "s@t.com", Phone = "0900000002", Password = "Password@1" });

        _audits.Should().BeEmpty();
        _uowMock.Verify(u => u.UserStore.CreateAsync(It.IsAny<User>()), Times.Once);
    }
}

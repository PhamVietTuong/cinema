using Cinema.Business.DTO.Catalog;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class TemplateManagerTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly TemplateManager _sut;

    public TemplateManagerTests()
    {
        _sut = new TemplateManager(_uowMock.Object);
    }

    [Fact]
    public async Task CreateAsync_DuplicateCode_Throws()
    {
        _uowMock.Setup(u => u.TemplateStore.ExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Template, bool>>>()))
            .ReturnsAsync(true);

        await FluentActions.Awaiting(() => _sut.CreateAsync(new CreateTemplateRequest
        {
            Code = "Dup", Name = "Dup", Channel = TemplateChannel.Sms, Body = "x"
        })).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_EmailWithoutSubject_Throws()
    {
        _uowMock.Setup(u => u.TemplateStore.ExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Template, bool>>>()))
            .ReturnsAsync(false);

        await FluentActions.Awaiting(() => _sut.CreateAsync(new CreateTemplateRequest
        {
            Code = "NewEmail", Name = "New", Channel = TemplateChannel.Email, Subject = null, Body = "x"
        })).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_SmsWithoutSubject_Succeeds()
    {
        _uowMock.Setup(u => u.TemplateStore.ExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Template, bool>>>()))
            .ReturnsAsync(false);
        _uowMock.Setup(u => u.TemplateStore.CreateAsync(It.IsAny<Template>())).ReturnsAsync((Template t) => t);

        var result = await _sut.CreateAsync(new CreateTemplateRequest
        {
            Code = "NewSms", Name = "New", Channel = TemplateChannel.Sms, Subject = null, Body = "x"
        });

        result.Code.Should().Be("NewSms");
    }

    [Fact]
    public async Task UpdateAsync_DoesNotChangeCode()
    {
        var entity = new Template { Id = Guid.NewGuid(), Code = "Original", Name = "Old", Channel = TemplateChannel.Sms, Body = "old" };
        _uowMock.Setup(u => u.TemplateStore.GetByIdAsync(entity.Id)).ReturnsAsync(entity);
        _uowMock.Setup(u => u.TemplateStore.UpdateAsync(It.IsAny<Template>())).ReturnsAsync((Template t) => t);

        var result = await _sut.UpdateAsync(new UpdateTemplateRequest
        {
            Id = entity.Id, Name = "New Name", Channel = TemplateChannel.Sms, Body = "new body"
        });

        result.Code.Should().Be("Original");
        result.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task UpdateAsync_EmailWithoutSubject_Throws()
    {
        var entity = new Template { Id = Guid.NewGuid(), Code = "Original", Name = "Old", Channel = TemplateChannel.Email, Subject = "Old Subject", Body = "old" };
        _uowMock.Setup(u => u.TemplateStore.GetByIdAsync(entity.Id)).ReturnsAsync(entity);

        await FluentActions.Awaiting(() => _sut.UpdateAsync(new UpdateTemplateRequest
        {
            Id = entity.Id, Name = "New Name", Channel = TemplateChannel.Email, Subject = null, Body = "new body"
        })).Should().ThrowAsync<InvalidOperationException>();
    }
}

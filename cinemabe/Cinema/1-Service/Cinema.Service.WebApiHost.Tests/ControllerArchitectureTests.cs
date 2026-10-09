using Microsoft.AspNetCore.Mvc;
using Cinema.Service.WebApiHost.Controllers;
using FluentAssertions;

namespace Cinema.Service.WebApiHost.Tests;

/// <summary>
/// Guards the single-controller architecture: the Web API host must expose exactly three controllers
/// (CinemaController, IdentityController, PaymentController), each in its own Swagger/NSwag group.
/// </summary>
public class ControllerArchitectureTests
{
    [Fact]
    public void OnlyThreeControllersExist()
    {
        var controllerNames = typeof(CinemaController).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToHashSet();

        controllerNames.Should().BeEquivalentTo(new[]
        {
            nameof(CinemaController),
            nameof(IdentityController),
            nameof(PaymentController)
        });
    }

    [Fact]
    public void EveryControllerIsInItsOwnSwaggerGroup()
    {
        GroupNameOf(typeof(CinemaController)).Should().Be("cinema");
        GroupNameOf(typeof(IdentityController)).Should().Be("identity");
        GroupNameOf(typeof(PaymentController)).Should().Be("payment");
    }

    private static string? GroupNameOf(Type controller)
    {
        return controller.GetCustomAttributes(typeof(ApiExplorerSettingsAttribute), inherit: false)
            .Cast<ApiExplorerSettingsAttribute>()
            .Single()
            .GroupName;
    }
}

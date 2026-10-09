using System.Reflection;
using System.Security.Claims;
using Cinema.Business.DTO.Auth;
using Cinema.Service.WebApiHost.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Cinema.Service.WebApiHost.Tests;

/// <summary>
/// Runs the real ASP.NET Core role authorization against the [Authorize] attributes on the staff endpoints. A
/// signed-in caller whose role is not allowed is a <c>Forbid</c> (HTTP 403), never a 401, which would log them out.
/// </summary>
public class StaffToolsetAuthorizationTests
{
    private static readonly ServiceProvider _services = new ServiceCollection()
        .AddLogging()
        .AddAuthorization()
        .BuildServiceProvider();

    private static ClaimsPrincipal Principal(string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role),
            new("theaterId", Guid.NewGuid().ToString())
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static async Task<bool> IsAllowedAsync(Type controller, string action, string role)
    {
        var method = controller.GetMethod(action);
        method.Should().NotBeNull($"{controller.Name}.{action} must exist");
        var attributes = method!.GetCustomAttributes<AuthorizeAttribute>().ToList();
        attributes.Should().NotBeEmpty($"{controller.Name}.{action} must be guarded");

        var provider = _services.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await AuthorizationPolicy.CombineAsync(provider, attributes);
        var result = await _services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(Principal(role), null, policy!);
        return result.Succeeded;
    }

    public static IEnumerable<object[]> ApproverOnlyActions()
    {
        foreach (var action in new[] { nameof(CinemaController.SaveShift), nameof(CinemaController.DeleteShift), nameof(CinemaController.GetRoster),
            nameof(CinemaController.GetTheaterStaff), nameof(CinemaController.GetTimeSheet), nameof(CinemaController.SaveTask), nameof(CinemaController.GetTasks) })
        {
            yield return new object[] { typeof(CinemaController), action };
        }
        foreach (var action in new[] { nameof(CinemaController.GetChecklistTemplates), nameof(CinemaController.SaveChecklistTemplate) })
        {
            yield return new object[] { typeof(CinemaController), action };
        }
    }

    public static IEnumerable<object[]> EveryStaffActions()
    {
        foreach (var action in new[] { nameof(CinemaController.ClockIn), nameof(CinemaController.ClockOut), nameof(CinemaController.GetMyClockStatus),
            nameof(CinemaController.GetMyShifts), nameof(CinemaController.GetMyTasks), nameof(CinemaController.SetMyTaskStatus) })
        {
            yield return new object[] { typeof(CinemaController), action };
        }
        foreach (var action in new[] { nameof(CinemaController.GetScheduleBoard), nameof(CinemaController.ReportIncident), nameof(CinemaController.GetIncidents),
            nameof(CinemaController.OpenChecklist), nameof(CinemaController.SetChecklistItem), nameof(CinemaController.CompleteChecklist) })
        {
            yield return new object[] { typeof(CinemaController), action };
        }
    }

    [Theory]
    [MemberData(nameof(ApproverOnlyActions))]
    public async Task TheaterStaff_CannotUseApproverEndpoints_SoRosterEditIs403(Type controller, string action)
    {
        (await IsAllowedAsync(controller, action, RoleNames.TheaterStaff)).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(ApproverOnlyActions))]
    public async Task Admin_CanUseApproverEndpoints(Type controller, string action)
    {
        (await IsAllowedAsync(controller, action, RoleNames.Admin)).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(EveryStaffActions))]
    public async Task EveryStaffRole_CanUseClockTasksIncidentsAndChecklists_ButCustomersCannot(Type controller, string action)
    {
        foreach (var role in RoleNames.StaffApp.Split(','))
        {
            (await IsAllowedAsync(controller, action, role)).Should().BeTrue($"{role} may call {action}");
        }
        (await IsAllowedAsync(controller, action, RoleNames.Customer)).Should().BeFalse();
    }
}

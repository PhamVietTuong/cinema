using Cinema.Business.DTO.Auth;
using FluentAssertions;

namespace Cinema.Business.Tests;

/// <summary>Pins the capability matrix (plan B4) so an accidental role change shows up as a failing test.</summary>
public class RoleNamesTests
{
    private static string[] Roles(string composite) => composite.Split(',');

    [Fact]
    public void StaffApp_IsAdminAndTheaterStaff_AndNotCustomer()
    {
        Roles(RoleNames.StaffApp).Should().BeEquivalentTo(new[] { RoleNames.Admin, RoleNames.TheaterStaff });
    }

    [Fact]
    public void Sellers_IsAdminAndTheaterStaff()
    {
        Roles(RoleNames.Sellers).Should().BeEquivalentTo(new[] { RoleNames.Admin, RoleNames.TheaterStaff });
    }

    [Fact]
    public void GateKeepers_Concession_AndBackOffice_MatchTheMatrix()
    {
        Roles(RoleNames.GateKeepers).Should().BeEquivalentTo(new[] { RoleNames.Admin, RoleNames.TheaterStaff });
        Roles(RoleNames.Concession).Should().BeEquivalentTo(new[] { RoleNames.Admin, RoleNames.TheaterStaff });
        Roles(RoleNames.BackOffice).Should().BeEquivalentTo(new[] { RoleNames.Admin, RoleNames.TheaterStaff });
    }

    [Fact]
    public void ApproversAndReporting_AreAdminOnly()
    {
        var expected = new[] { RoleNames.Admin };
        Roles(RoleNames.Approvers).Should().BeEquivalentTo(expected);
        Roles(RoleNames.Reporting).Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void StockApprovers_AreAdminOnly()
    {
        Roles(RoleNames.StockApprovers).Should().BeEquivalentTo(new[] { RoleNames.Admin });
    }

    [Fact]
    public void TheaterScopedRoles_IsTheaterStaffOnly()
    {
        Roles(RoleNames.TheaterScopedRoles).Should().BeEquivalentTo(new[] { RoleNames.TheaterStaff });
    }
}

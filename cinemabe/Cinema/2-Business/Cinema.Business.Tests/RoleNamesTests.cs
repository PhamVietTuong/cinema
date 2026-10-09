using Cinema.Business.DTO.Auth;
using FluentAssertions;

namespace Cinema.Business.Tests;

/// <summary>Pins the capability matrix (plan B4) so an accidental role change shows up as a failing test.</summary>
public class RoleNamesTests
{
    private static string[] Roles(string composite) => composite.Split(',');

    [Fact]
    public void StaffApp_IsEveryStaffRole_AndNotCustomer()
    {
        Roles(RoleNames.StaffApp).Should().BeEquivalentTo(new[]
        {
            RoleNames.Admin, RoleNames.RegionalManager, RoleNames.TheaterManager, RoleNames.TheaterStaff,
            RoleNames.BoxOfficeStaff, RoleNames.GateStaff, RoleNames.KitchenStaff
        });
    }

    [Fact]
    public void Sellers_ExcludeGateAndKitchen()
    {
        Roles(RoleNames.Sellers).Should().BeEquivalentTo(new[]
        {
            RoleNames.Admin, RoleNames.TheaterManager, RoleNames.TheaterStaff, RoleNames.BoxOfficeStaff
        });
    }

    [Fact]
    public void GateKeepers_Concession_AndBackOffice_MatchTheMatrix()
    {
        Roles(RoleNames.GateKeepers).Should().BeEquivalentTo(new[]
        {
            RoleNames.Admin, RoleNames.TheaterManager, RoleNames.TheaterStaff, RoleNames.GateStaff, RoleNames.BoxOfficeStaff
        });
        Roles(RoleNames.Concession).Should().BeEquivalentTo(new[]
        {
            RoleNames.Admin, RoleNames.TheaterManager, RoleNames.TheaterStaff, RoleNames.KitchenStaff, RoleNames.BoxOfficeStaff
        });
        Roles(RoleNames.BackOffice).Should().BeEquivalentTo(new[]
        {
            RoleNames.Admin, RoleNames.TheaterManager, RoleNames.TheaterStaff, RoleNames.KitchenStaff
        });
        Roles(RoleNames.BackOffice).Should().NotContain(RoleNames.GateStaff);
    }

    [Fact]
    public void ApproversAndReporting_AreAdminRegionalAndManager()
    {
        var expected = new[] { RoleNames.Admin, RoleNames.RegionalManager, RoleNames.TheaterManager };
        Roles(RoleNames.Approvers).Should().BeEquivalentTo(expected);
        Roles(RoleNames.Reporting).Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void StockApprovers_AreUnchanged()
    {
        Roles(RoleNames.StockApprovers).Should().BeEquivalentTo(new[] { RoleNames.Admin, RoleNames.TheaterManager });
    }

    [Fact]
    public void TheaterScopedRoles_AreEveryStaffRoleExceptAdminAndRegional()
    {
        Roles(RoleNames.TheaterScopedRoles).Should().BeEquivalentTo(new[]
        {
            RoleNames.TheaterManager, RoleNames.TheaterStaff, RoleNames.BoxOfficeStaff, RoleNames.GateStaff, RoleNames.KitchenStaff
        });
    }
}

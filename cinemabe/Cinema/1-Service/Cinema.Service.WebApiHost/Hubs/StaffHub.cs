using Cinema.Business.DTO.Auth;
using Cinema.Service.WebApiHost.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Cinema.Service.WebApiHost.Hubs;

/// <summary>
/// Real-time channel of the staff app (<c>/hubs/staff</c>). Server-to-client events: <c>FoodOrderQueued</c>,
/// <c>FoodOrderUpdated</c>, <c>StockLow</c>, <c>IncidentRaised</c>, each sent to the <c>theater:{id}</c> group.
/// A connection joins the groups of its own theater claim(s) on connect; an admin has none and joins the theaters
/// it wants with <see cref="JoinTheater"/>. The JWT arrives in the <c>access_token</c> query string.
/// </summary>
[Authorize(Roles = RoleNames.StaffApp)]
public class StaffHub : Hub
{
    public const string FoodOrderQueuedEvent = "FoodOrderQueued";
    public const string FoodOrderUpdatedEvent = "FoodOrderUpdated";
    public const string StockLowEvent = "StockLow";
    public const string IncidentRaisedEvent = "IncidentRaised";

    public static string TheaterGroup(Guid theaterId)
    {
        return $"theater:{theaterId}";
    }

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user != null)
        {
            foreach (var theaterId in user.GetTheaterIds())
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, TheaterGroup(theaterId));
            }
        }
        await base.OnConnectedAsync();
    }

    /// <summary>Subscribes to a theater's events. Admin: any theater. Others: only a theater in their claims (else a HubException).</summary>
    public async Task JoinTheater(Guid theaterId)
    {
        EnsureMayListen(theaterId);
        await Groups.AddToGroupAsync(Context.ConnectionId, TheaterGroup(theaterId));
    }

    public async Task LeaveTheater(Guid theaterId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, TheaterGroup(theaterId));
    }

    private void EnsureMayListen(Guid theaterId)
    {
        var user = Context.User;
        if (user != null && (user.IsAdmin() || user.GetTheaterIds().Contains(theaterId)))
        {
            return;
        }
        throw new HubException("You do not have access to this theater.");
    }
}

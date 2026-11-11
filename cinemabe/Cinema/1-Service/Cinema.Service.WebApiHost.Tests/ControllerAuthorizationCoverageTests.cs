using System.Reflection;
using Cinema.Service.WebApiHost.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.SignalR;

namespace Cinema.Service.WebApiHost.Tests;

/// <summary>
/// Every HTTP action must be guarded by <c>[Authorize]</c> (on the action or its controller) or be listed in
/// <see cref="AnonymousAllowList"/>. A new public action with neither fails here by name, so an endpoint can never
/// become anonymous by accident.
/// </summary>
public class ControllerAuthorizationCoverageTests
{
    /// <summary>The only actions that may be called without signing in. Adding one is a deliberate, reviewed decision.</summary>
    private static readonly HashSet<string> AnonymousAllowList = new()
    {
        // CinemaController (public catalog and storefront reads)
        Key(nameof(CinemaController), nameof(CinemaController.GetActiveSplashBanner)),
        Key(nameof(CinemaController), nameof(CinemaController.GetAgeRestriction)),
        Key(nameof(CinemaController), nameof(CinemaController.GetAgeRestrictions)),
        Key(nameof(CinemaController), nameof(CinemaController.GetComboComponents)),
        Key(nameof(CinemaController), nameof(CinemaController.GetComingSoonMovies)),
        Key(nameof(CinemaController), nameof(CinemaController.GetEffectiveLoyaltyRate)),
        Key(nameof(CinemaController), nameof(CinemaController.GetFoodAndDrink)),
        Key(nameof(CinemaController), nameof(CinemaController.GetFoodAndDrinks)),
        Key(nameof(CinemaController), nameof(CinemaController.GetHoliday)),
        Key(nameof(CinemaController), nameof(CinemaController.GetHolidays)),
        Key(nameof(CinemaController), nameof(CinemaController.GetMemberShip)),
        Key(nameof(CinemaController), nameof(CinemaController.GetMemberShips)),
        Key(nameof(CinemaController), nameof(CinemaController.GetMovie)),
        Key(nameof(CinemaController), nameof(CinemaController.GetMovieBySlug)),
        Key(nameof(CinemaController), nameof(CinemaController.GetMovieComments)),
        Key(nameof(CinemaController), nameof(CinemaController.GetMovieLanguages)),
        Key(nameof(CinemaController), nameof(CinemaController.GetMovies)),
        Key(nameof(CinemaController), nameof(CinemaController.GetMovieType)),
        Key(nameof(CinemaController), nameof(CinemaController.GetMovieTypeDetails)),
        Key(nameof(CinemaController), nameof(CinemaController.GetMovieTypes)),
        Key(nameof(CinemaController), nameof(CinemaController.GetNews)),
        Key(nameof(CinemaController), nameof(CinemaController.GetNewsList)),
        Key(nameof(CinemaController), nameof(CinemaController.GetNowShowingMovies)),
        Key(nameof(CinemaController), nameof(CinemaController.GetPatronCategories)),
        Key(nameof(CinemaController), nameof(CinemaController.GetPatronCategoriesByTheater)),
        Key(nameof(CinemaController), nameof(CinemaController.GetPatronCategory)),
        Key(nameof(CinemaController), nameof(CinemaController.GetPeopleByNames)),
        Key(nameof(CinemaController), nameof(CinemaController.GetPromotionCampaign)),
        Key(nameof(CinemaController), nameof(CinemaController.GetPromotionCampaigns)),
        Key(nameof(CinemaController), nameof(CinemaController.GetPublishedPromotionPrograms)),
        Key(nameof(CinemaController), nameof(CinemaController.GetRecommendedMovies)),
        Key(nameof(CinemaController), nameof(CinemaController.GetRoom)),
        Key(nameof(CinemaController), nameof(CinemaController.GetRooms)),
        Key(nameof(CinemaController), nameof(CinemaController.GetRoomType)),
        Key(nameof(CinemaController), nameof(CinemaController.GetRoomTypes)),
        Key(nameof(CinemaController), nameof(CinemaController.GetSeatType)),
        Key(nameof(CinemaController), nameof(CinemaController.GetSeatTypes)),
        Key(nameof(CinemaController), nameof(CinemaController.GetShowTime)),
        Key(nameof(CinemaController), nameof(CinemaController.GetShowTimeList)),
        Key(nameof(CinemaController), nameof(CinemaController.GetShowTimes)),
        Key(nameof(CinemaController), nameof(CinemaController.GetTheater)),
        Key(nameof(CinemaController), nameof(CinemaController.GetTheaters)),
        Key(nameof(CinemaController), nameof(CinemaController.GetTheatersByMovie)),
        Key(nameof(CinemaController), nameof(CinemaController.GetTicketPrice)),
        Key(nameof(CinemaController), nameof(CinemaController.GetTicketPrices)),
        Key(nameof(CinemaController), nameof(CinemaController.GetTimeSlot)),
        Key(nameof(CinemaController), nameof(CinemaController.GetTimeSlots)),
        Key(nameof(CinemaController), nameof(CinemaController.GetUserType)),
        Key(nameof(CinemaController), nameof(CinemaController.GetUserTypes)),
        Key(nameof(CinemaController), nameof(CinemaController.SearchShowTimes)),
        // IdentityController (sign-in, registration, password reset)
        Key(nameof(IdentityController), nameof(IdentityController.ConfirmEmail)),
        Key(nameof(IdentityController), nameof(IdentityController.FacebookLogin)),
        Key(nameof(IdentityController), nameof(IdentityController.ForgotPassword)),
        Key(nameof(IdentityController), nameof(IdentityController.GoogleLogin)),
        Key(nameof(IdentityController), nameof(IdentityController.Login)),
        Key(nameof(IdentityController), nameof(IdentityController.Register)),
        Key(nameof(IdentityController), nameof(IdentityController.ResendVerification)),
        Key(nameof(IdentityController), nameof(IdentityController.ResetPassword)),
        Key(nameof(IdentityController), nameof(IdentityController.VerifyTwoFactor)),
        // PaymentController (public seat map, prices and the signed gateway callback)
        Key(nameof(PaymentController), nameof(PaymentController.GetSeats)),
        Key(nameof(PaymentController), nameof(PaymentController.GetShowTimePrices)),
        Key(nameof(PaymentController), nameof(PaymentController.PaymentCallback)),
    };

    private static readonly Type[] Controllers = { typeof(CinemaController), typeof(IdentityController), typeof(PaymentController) };

    private static string Key(string controller, string action) => $"{controller}.{action}";

    private static IEnumerable<(Type Controller, MethodInfo Action)> Actions()
    {
        foreach (var controller in Controllers)
        {
            var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes(inherit: true).Any(a => a is IActionHttpMethodProvider || a is IRouteTemplateProvider));
            foreach (var action in actions)
            {
                yield return (controller, action);
            }
        }
    }

    /// <summary>Anonymous = not guarded by [Authorize], or opened by [AllowAnonymous] on the action or its controller.</summary>
    private static bool IsAnonymous(Type controller, MethodInfo action)
    {
        var guarded = action.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any()
            || controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();
        var opened = action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any()
            || controller.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();
        return !guarded || opened;
    }

    private static HashSet<string> AnonymousActions()
    {
        return Actions()
            .Where(a => IsAnonymous(a.Controller, a.Action))
            .Select(a => Key(a.Controller.Name, a.Action.Name))
            .ToHashSet();
    }

    [Trait("Req", "BR-AUTH-01")]
    [Fact]
    public void EveryAction_IsGuarded_OrOnTheAnonymousAllowList()
    {
        var unguarded = AnonymousActions().Where(k => !AnonymousAllowList.Contains(k)).OrderBy(k => k).ToList();

        unguarded.Should().BeEmpty("these actions are anonymous: add [Authorize] or list them in AnonymousAllowList on purpose");
    }

    [Trait("Req", "BR-AUTH-01")]
    [Fact]
    public void AnonymousAllowList_HasNoStaleEntries()
    {
        var actualAnonymous = AnonymousActions();

        AnonymousAllowList.Where(k => !actualAnonymous.Contains(k)).Should()
            .BeEmpty("allow-list entries must name an action that exists and is really anonymous");
    }

    [Trait("Req", "BR-AUTH-01")]
    [Fact]
    public void EveryHub_RequiresAuthorization()
    {
        var hubs = typeof(CinemaController).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(Hub).IsAssignableFrom(t))
            .ToList();

        hubs.Should().NotBeEmpty();
        hubs.Where(h => !h.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any()).Select(h => h.Name)
            .Should().BeEmpty("every SignalR hub must carry [Authorize]");
    }
}

using System.Reflection;
using Cinema.Service.WebApiHost.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;

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
        "CinemaController.GetActiveSplashBanner",
        "CinemaController.GetAgeRestriction",
        "CinemaController.GetAgeRestrictions",
        "CinemaController.GetComboComponents",
        "CinemaController.GetComingSoonMovies",
        "CinemaController.GetEffectiveLoyaltyRate",
        "CinemaController.GetFoodAndDrink",
        "CinemaController.GetFoodAndDrinks",
        "CinemaController.GetHoliday",
        "CinemaController.GetHolidays",
        "CinemaController.GetMemberShip",
        "CinemaController.GetMemberShips",
        "CinemaController.GetMovie",
        "CinemaController.GetMovieBySlug",
        "CinemaController.GetMovieComments",
        "CinemaController.GetMovieLanguages",
        "CinemaController.GetMovies",
        "CinemaController.GetMovieType",
        "CinemaController.GetMovieTypeDetails",
        "CinemaController.GetMovieTypes",
        "CinemaController.GetNews",
        "CinemaController.GetNewsList",
        "CinemaController.GetNowShowingMovies",
        "CinemaController.GetPatronCategories",
        "CinemaController.GetPatronCategoriesByTheater",
        "CinemaController.GetPatronCategory",
        "CinemaController.GetPeopleByNames",
        "CinemaController.GetPromotionCampaign",
        "CinemaController.GetPromotionCampaigns",
        "CinemaController.GetPublishedPromotionPrograms",
        "CinemaController.GetRecommendedMovies",
        "CinemaController.GetRoom",
        "CinemaController.GetRooms",
        "CinemaController.GetRoomType",
        "CinemaController.GetRoomTypes",
        "CinemaController.GetSeatType",
        "CinemaController.GetSeatTypes",
        "CinemaController.GetShowTime",
        "CinemaController.GetShowTimeList",
        "CinemaController.GetShowTimes",
        "CinemaController.GetTheater",
        "CinemaController.GetTheaters",
        "CinemaController.GetTheatersByMovie",
        "CinemaController.GetTicketPrice",
        "CinemaController.GetTicketPrices",
        "CinemaController.GetTimeSlot",
        "CinemaController.GetTimeSlots",
        "CinemaController.GetUserType",
        "CinemaController.GetUserTypes",
        "CinemaController.SearchShowTimes",
        // IdentityController (sign-in, registration, password reset)
        "IdentityController.ConfirmEmail",
        "IdentityController.FacebookLogin",
        "IdentityController.ForgotPassword",
        "IdentityController.GoogleLogin",
        "IdentityController.Login",
        "IdentityController.Register",
        "IdentityController.ResendVerification",
        "IdentityController.ResetPassword",
        "IdentityController.VerifyTwoFactor",
        // PaymentController (public seat map, prices and the signed gateway callback)
        "PaymentController.GetSeats",
        "PaymentController.GetShowTimePrices",
        "PaymentController.PaymentCallback",
    };

    private static readonly Type[] Controllers = { typeof(CinemaController), typeof(IdentityController), typeof(PaymentController) };

    private static IEnumerable<(Type Controller, MethodInfo Action)> Actions()
    {
        foreach (var controller in Controllers)
        {
            var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any());
            foreach (var action in actions)
            {
                yield return (controller, action);
            }
        }
    }

    private static string Key(Type controller, MethodInfo action) => $"{controller.Name}.{action.Name}";

    private static bool IsGuarded(Type controller, MethodInfo action)
    {
        return action.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any()
            || controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();
    }

    private static bool IsOpenedByAllowAnonymous(Type controller, MethodInfo action)
    {
        return action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();
    }

    [Trait("Req", "BR-AUTH-01")]
    [Fact]
    public void EveryAction_IsGuarded_OrOnTheAnonymousAllowList()
    {
        var unguarded = Actions()
            .Where(a => (!IsGuarded(a.Controller, a.Action) || IsOpenedByAllowAnonymous(a.Controller, a.Action)))
            .Select(a => Key(a.Controller, a.Action))
            .Where(k => !AnonymousAllowList.Contains(k))
            .OrderBy(k => k)
            .ToList();

        unguarded.Should().BeEmpty("these actions are anonymous: add [Authorize] or list them in AnonymousAllowList on purpose");
    }

    [Trait("Req", "BR-AUTH-01")]
    [Fact]
    public void AnonymousAllowList_HasNoStaleEntries()
    {
        var actualAnonymous = Actions()
            .Where(a => !IsGuarded(a.Controller, a.Action) || IsOpenedByAllowAnonymous(a.Controller, a.Action))
            .Select(a => Key(a.Controller, a.Action))
            .ToHashSet();

        AnonymousAllowList.Where(k => !actualAnonymous.Contains(k)).Should()
            .BeEmpty("allow-list entries must name an action that exists and is really anonymous");
    }
}

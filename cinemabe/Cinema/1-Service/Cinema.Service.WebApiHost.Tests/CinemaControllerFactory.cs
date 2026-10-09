using System.Security.Claims;
using Cinema.Business.Contracts;
using Cinema.Service.WebApiHost.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cinema.Service.WebApiHost.Tests;

/// <summary>
/// Builds a <see cref="CinemaController"/> for unit tests, passing <c>null!</c> for every
/// constructor dependency the caller does not need and wiring the given <see cref="ClaimsPrincipal"/>
/// as the request's authenticated user.
/// </summary>
internal static class CinemaControllerFactory
{
    public static CinemaController Create(
        ClaimsPrincipal user,
        IGateManager? gate = null,
        IBoxOfficeManager? boxOffice = null,
        ICustomerServiceManager? customerService = null,
        IStaffReportManager? reports = null,
        IDailyCloseManager? dailyClose = null,
        IScheduleBoardManager? board = null,
        IIncidentManager? incidents = null,
        IChecklistManager? checklists = null,
        IManagerOverrideService? overrides = null,
        IWorkforceManager? workforce = null)
    {
        var controller = new CinemaController(
            movieManager: null!,
            theaterManager: null!,
            ageRestrictions: null!,
            discountTypes: null!,
            movieTypes: null!,
            seatTypes: null!,
            userTypes: null!,
            memberShips: null!,
            holidays: null!,
            news: null!,
            discounts: null!,
            foodAndDrinks: null!,
            rooms: null!,
            roomTypes: null!,
            showTimes: null!,
            movieTypeDetails: null!,
            invoices: null!,
            timeSlots: null!,
            ticketPrices: null!,
            patronCategories: null!,
            roomTypePatronCategoryPrices: null!,
            combos: null!,
            env: null!,
            concessions: null!,
            gate: gate!,
            boxOffice: boxOffice!,
            customerService: customerService!,
            reports: reports!,
            dailyClose: dailyClose!,
            board: board!,
            incidents: incidents!,
            checklists: checklists!,
            overrides: overrides!,
            workforce: workforce!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };
        return controller;
    }
}

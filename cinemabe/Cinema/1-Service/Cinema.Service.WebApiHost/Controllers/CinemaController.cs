using Cinema.Business.Contracts;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.BoxOffice;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Concession;
using Cinema.Business.DTO.CustomerService;
using Cinema.Business.DTO.Gate;
using Cinema.Business.DTO.Inventory;
using Cinema.Business.DTO.Movies;
using Cinema.Business.DTO.Operations;
using Cinema.Business.DTO.Requests;
using Cinema.Business.DTO.Staff;
using Cinema.Business.DTO.Theaters;
using Cinema.Data.Entities;
using Cinema.Foundation.Logging;
using Cinema.Service.WebApiHost.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cinema.Service.WebApiHost.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
[ApiExplorerSettings(GroupName = "cinema")]
public class CinemaController : ApiControllerBase
{
    private const string _adminRole = "Admin";
    private const string _theaterIdFilter = "theaterId";

    private readonly IMovieManager   _movieManager;
    private readonly ITheaterManager _theaterManager;
    private readonly IAgeRestrictionManager _ageRestrictions;
    private readonly IDiscountTypeManager   _discountTypes;
    private readonly IMovieTypeManager      _movieTypes;
    private readonly ISeatTypeManager       _seatTypes;
    private readonly IUserTypeManager       _userTypes;
    private readonly IMemberShipManager     _memberShips;
    private readonly IHolidayManager        _holidays;
    private readonly INewsManager           _news;
    private readonly IDiscountManager       _discounts;
    private readonly IFoodAndDrinkManager   _foodAndDrinks;
    private readonly IRoomManager           _rooms;
    private readonly IRoomTypeManager       _roomTypes;
    private readonly IShowTimeManager       _showTimes;
    private readonly IMovieTypeDetailManager     _movieTypeDetails;
    private readonly IInvoiceAdminManager        _invoices;
    private readonly ITimeSlotManager            _timeSlots;
    private readonly ITicketPriceManager         _ticketPrices;
    private readonly IPatronCategoryManager      _patronCategories;
    private readonly IRoomTypePatronCategoryPriceManager _roomTypePatronCategoryPrices;
    private readonly IComboManager               _combos;
    private readonly IWebHostEnvironment         _env;

    // Staff
    private readonly IConcessionManager _concessions;
    private readonly IGateManager _gate;
    private readonly IBoxOfficeManager _boxOffice;
    private readonly ICustomerServiceManager _customerService;
    private readonly IStaffReportManager _reports;
    private readonly IDailyCloseManager _dailyClose;
    private readonly IScheduleBoardManager _board;
    private readonly IIncidentManager _incidents;
    private readonly IChecklistManager _checklists;
    private readonly IManagerOverrideService _overrides;
    private readonly IWorkforceManager _workforce;

    public CinemaController(
        IMovieManager movieManager,
        ITheaterManager theaterManager,
        IAgeRestrictionManager ageRestrictions,
        IDiscountTypeManager discountTypes,
        IMovieTypeManager movieTypes,
        ISeatTypeManager seatTypes,
        IUserTypeManager userTypes,
        IMemberShipManager memberShips,
        IHolidayManager holidays,
        INewsManager news,
        IDiscountManager discounts,
        IFoodAndDrinkManager foodAndDrinks,
        IRoomManager rooms,
        IRoomTypeManager roomTypes,
        IShowTimeManager showTimes,
        IMovieTypeDetailManager movieTypeDetails,
        IInvoiceAdminManager invoices,
        ITimeSlotManager timeSlots,
        ITicketPriceManager ticketPrices,
        IPatronCategoryManager patronCategories,
        IRoomTypePatronCategoryPriceManager roomTypePatronCategoryPrices,
        IComboManager combos,
        IWebHostEnvironment env,
        // Staff
        IConcessionManager concessions,
        IGateManager gate,
        IBoxOfficeManager boxOffice,
        ICustomerServiceManager customerService,
        IStaffReportManager reports,
        IDailyCloseManager dailyClose,
        IScheduleBoardManager board,
        IIncidentManager incidents,
        IChecklistManager checklists,
        IManagerOverrideService overrides,
        IWorkforceManager workforce)
    {
        _combos              = combos;
        _concessions         = concessions;
        _gate                = gate;
        _boxOffice           = boxOffice;
        _customerService     = customerService;
        _reports             = reports;
        _dailyClose          = dailyClose;
        _board               = board;
        _incidents           = incidents;
        _checklists          = checklists;
        _overrides           = overrides;
        _workforce           = workforce;
        _movieManager    = movieManager;
        _theaterManager  = theaterManager;
        _ageRestrictions = ageRestrictions;
        _discountTypes   = discountTypes;
        _movieTypes      = movieTypes;
        _seatTypes       = seatTypes;
        _userTypes       = userTypes;
        _memberShips     = memberShips;
        _holidays        = holidays;
        _news            = news;
        _discounts       = discounts;
        _foodAndDrinks   = foodAndDrinks;
        _rooms           = rooms;
        _roomTypes       = roomTypes;
        _showTimes       = showTimes;
        _movieTypeDetails    = movieTypeDetails;
        _invoices            = invoices;
        _timeSlots           = timeSlots;
        _ticketPrices        = ticketPrices;
        _patronCategories    = patronCategories;
        _roomTypePatronCategoryPrices = roomTypePatronCategoryPrices;
        _env                 = env;
    }

    // ── Uploads ─────────────────────────────────────────────────────────────────

    private static readonly HashSet<string> _allowedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    private const long _maxImageBytes = 5 * 1024 * 1024; // 5 MB

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(UploadResultDTO), 200)]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        LogProvider.Current.Information($"{GetType().Name}.UploadImage being awakened to process request...");
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }
            if (file.Length > _maxImageBytes)
            {
                return BadRequest("File exceeds the 5 MB limit.");
            }

            var ext = Path.GetExtension(file.FileName);
            if (!_allowedImageExtensions.Contains(ext) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Only image files (jpg, png, webp, gif) are allowed.");
            }

            var uploadsDir = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads");
            Directory.CreateDirectory(uploadsDir);

            var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            var fullPath = Path.Combine(uploadsDir, fileName);
            await using (var stream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(stream);
            }

            var url = $"{Request.Scheme}://{Request.Host}/uploads/{fileName}";
            return Ok(new UploadResultDTO { Url = url });
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(UploadImage));
        }
    }

    // ── Movies ────────────────────────────────────────────────────────────────

    [HttpPost]
    [ProducesResponseType(typeof(List<MovieDTO>), 200)]
    public async Task<IActionResult> GetRecommendedMovies([FromQuery] int count = 8)
    {
        LogProvider.Current.Information($"{GetType().Name}.GetRecommendedMovies being awakened to process request...");
        try
        {
            // Personalise for a signed-in user; anonymous callers get top-rated picks.
            Guid? userId = User?.Identity?.IsAuthenticated == true ? User.GetUserId() : null;
            var result = await _movieManager.GetRecommendedAsync(userId, count);
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetRecommendedMovies));
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<MovieDTO>), 200)]
    public async Task<IActionResult> GetMovies([FromBody] PagingSearchDTO search)
    {
        LogProvider.Current.Information($"{GetType().Name}.GetMovies being awakened to process request...");
        try
        {
            var result = await _movieManager.GetMoviesAsync(search);
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetMovies));
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<MovieDTO>), 200)]
    public async Task<IActionResult> GetNowShowingMovies([FromBody] PagingSearchDTO search)
    {
        LogProvider.Current.Information($"{GetType().Name}.GetNowShowingMovies being awakened to process request...");
        try
        {
            var result = await _movieManager.GetNowShowingAsync(search);
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetNowShowingMovies));
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<MovieDTO>), 200)]
    public async Task<IActionResult> GetComingSoonMovies([FromBody] PagingSearchDTO search)
    {
        LogProvider.Current.Information($"{GetType().Name}.GetComingSoonMovies being awakened to process request...");
        try
        {
            var result = await _movieManager.GetComingSoonAsync(search);
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetComingSoonMovies));
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(MovieDetailDTO), 200)]
    public async Task<IActionResult> GetMovie([FromQuery] Guid id)
    {
        LogProvider.Current.Information($"{GetType().Name}.GetMovie being awakened to process request...");
        try
        {
            var result = await _movieManager.GetDetailAsync(id);
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetMovie));
        }
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(MovieDetailDTO), 200)]
    public async Task<IActionResult> CreateMovie([FromBody] CreateMovieRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.CreateMovie being awakened to process request...");
        try
        {
            var result = await _movieManager.CreateAsync(request);
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(CreateMovie));
        }
    }

    [Authorize(Roles = _adminRole)]
    [HttpPut]
    [ProducesResponseType(typeof(MovieDetailDTO), 200)]
    public async Task<IActionResult> UpdateMovie([FromBody] UpdateMovieRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.UpdateMovie being awakened to process request...");
        try
        {
            var result = await _movieManager.UpdateAsync(request);
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(UpdateMovie));
        }
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteMovie([FromQuery] Guid id)
    {
        LogProvider.Current.Information($"{GetType().Name}.DeleteMovie being awakened to process request...");
        try
        {
            await _movieManager.DeleteAsync(id);
            return NoContent();
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(DeleteMovie));
        }
    }

    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(CommentDTO), 200)]
    public async Task<IActionResult> AddComment([FromQuery] Guid movieId, [FromBody] AddCommentRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.AddComment being awakened to process request...");
        try
        {
            var result = await _movieManager.AddCommentAsync(movieId, User.GetUserId(), request.Content, request.ParentId);
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(AddComment));
        }
    }

    [Authorize]
    [HttpPost]
    [ProducesResponseType(204)]
    public async Task<IActionResult> RateMovie([FromQuery] Guid movieId, [FromBody] RateMovieRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.RateMovie being awakened to process request...");
        try
        {
            await _movieManager.RateMovieAsync(movieId, User.GetUserId(), request.Score, request.Review);
            return NoContent();
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(RateMovie));
        }
    }

    // ── Comment moderation (admin) ──────────────────────────────────────────────

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<CommentModerationDTO>), 200)]
    public async Task<IActionResult> GetCommentsForModeration([FromBody] PagingSearchDTO search)
    {
        LogProvider.Current.Information($"{GetType().Name}.GetCommentsForModeration being awakened to process request...");
        try
        {
            var result = await _movieManager.GetCommentsForModerationAsync(search);
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetCommentsForModeration));
        }
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ModerateComment([FromBody] ModerateCommentRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.ModerateComment being awakened to process request...");
        try
        {
            var ok = await _movieManager.ModerateCommentAsync(request.CommentId, request.Approved);
            return ok ? Ok(new { message = "Comment updated." }) : NotFound(new { error = "Comment not found." });
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(ModerateComment));
        }
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteComment([FromBody] DeleteCommentRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.DeleteComment being awakened to process request...");
        try
        {
            var ok = await _movieManager.DeleteCommentAsync(request.CommentId);
            return ok ? Ok(new { message = "Comment deleted." }) : NotFound(new { error = "Comment not found." });
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(DeleteComment));
        }
    }

    // ── Theaters ──────────────────────────────────────────────────────────────

    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<TheaterDTO>), 200)]
    public async Task<IActionResult> GetTheaters([FromBody] PagingSearchDTO search)
    {
        LogProvider.Current.Information($"{GetType().Name}.GetTheaters being awakened to process request...");
        try
        {
            var result = await _theaterManager.GetTheatersAsync(search);
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetTheaters));
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(TheaterDTO), 200)]
    public async Task<IActionResult> GetTheater([FromQuery] Guid id)
    {
        LogProvider.Current.Information($"{GetType().Name}.GetTheater being awakened to process request...");
        try
        {
            var result = await _theaterManager.GetByIdAsync(id);
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetTheater));
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<TheaterDTO>), 200)]
    public async Task<IActionResult> GetTheatersByMovie([FromBody] PagingSearchDTO search)
    {
        LogProvider.Current.Information($"{GetType().Name}.GetTheatersByMovie being awakened to process request...");
        try
        {
            var result = await _theaterManager.GetTheatersByMovieAsync(search);
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetTheatersByMovie));
        }
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(TheaterDTO), 200)]
    public async Task<IActionResult> CreateTheater([FromBody] CreateTheaterRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.CreateTheater being awakened to process request...");
        try
        {
            var result = await _theaterManager.CreateAsync(request);
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(CreateTheater));
        }
    }

    [Authorize(Roles = _adminRole)]
    [HttpPut]
    [ProducesResponseType(typeof(TheaterDTO), 200)]
    public async Task<IActionResult> UpdateTheater([FromBody] UpdateTheaterRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.UpdateTheater being awakened to process request...");
        try
        {
            var result = await _theaterManager.UpdateAsync(request);
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(UpdateTheater));
        }
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteTheater([FromQuery] Guid id)
    {
        LogProvider.Current.Information($"{GetType().Name}.DeleteTheater being awakened to process request...");
        try
        {
            await _theaterManager.DeleteAsync(id);
            return NoContent();
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(DeleteTheater));
        }
    }

    // ── ShowTimes ─────────────────────────────────────────────────────────────

    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<ShowTimeListDTO>), 200)]
    public async Task<IActionResult> GetShowTimes([FromBody] PagingSearchDTO search)
    {
        LogProvider.Current.Information($"{GetType().Name}.GetShowTimes being awakened to process request...");
        try
        {
            var result = await _movieManager.GetShowTimesAsync(search);
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetShowTimes));
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  Catalog (simple lookup) CRUD — reads public, writes Admin-only.
    // ════════════════════════════════════════════════════════════════════════════

    #region AgeRestriction
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<AgeRestrictionDTO>), 200)]
    public Task<IActionResult> GetAgeRestrictions([FromBody] PagingSearchDTO search)
    {
        return Run(nameof(GetAgeRestrictions), () => _ageRestrictions.GetAsync(search));
    }

    [HttpGet]
    [ProducesResponseType(typeof(AgeRestrictionDTO), 200)]
    public async Task<IActionResult> GetAgeRestriction([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _ageRestrictions.ExistsAsync(id), nameof(GetAgeRestriction), nameof(AgeRestriction), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(GetAgeRestriction), () => _ageRestrictions.GetByIdAsync(id));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(AgeRestrictionDTO), 200)]
    public Task<IActionResult> CreateAgeRestriction([FromBody] CreateAgeRestrictionRequest request)
    {
        return Run(nameof(CreateAgeRestriction), () => _ageRestrictions.CreateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPut]
    [ProducesResponseType(typeof(AgeRestrictionDTO), 200)]
    public async Task<IActionResult> UpdateAgeRestriction([FromBody] UpdateAgeRestrictionRequest request)
    {
        var notFound = await EnsureExistsAsync(() => _ageRestrictions.ExistsAsync(request.Id), nameof(UpdateAgeRestriction), nameof(AgeRestriction), request.Id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(UpdateAgeRestriction), () => _ageRestrictions.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteAgeRestriction([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _ageRestrictions.ExistsAsync(id), nameof(DeleteAgeRestriction), nameof(AgeRestriction), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await RunNoContent(nameof(DeleteAgeRestriction), () => _ageRestrictions.DeleteAsync(id));
    }
    #endregion

    #region DiscountType
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<DiscountTypeDTO>), 200)]
    public Task<IActionResult> GetDiscountTypes([FromBody] PagingSearchDTO search)
    {
        return Run(nameof(GetDiscountTypes), () => _discountTypes.GetAsync(search));
    }

    [HttpGet]
    [ProducesResponseType(typeof(DiscountTypeDTO), 200)]
    public async Task<IActionResult> GetDiscountType([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _discountTypes.ExistsAsync(id), nameof(GetDiscountType), nameof(DiscountType), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(GetDiscountType), () => _discountTypes.GetByIdAsync(id));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(DiscountTypeDTO), 200)]
    public Task<IActionResult> CreateDiscountType([FromBody] CreateDiscountTypeRequest request)
    {
        return Run(nameof(CreateDiscountType), () => _discountTypes.CreateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPut]
    [ProducesResponseType(typeof(DiscountTypeDTO), 200)]
    public async Task<IActionResult> UpdateDiscountType([FromBody] UpdateDiscountTypeRequest request)
    {
        var notFound = await EnsureExistsAsync(() => _discountTypes.ExistsAsync(request.Id), nameof(UpdateDiscountType), nameof(DiscountType), request.Id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(UpdateDiscountType), () => _discountTypes.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteDiscountType([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _discountTypes.ExistsAsync(id), nameof(DeleteDiscountType), nameof(DiscountType), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await RunNoContent(nameof(DeleteDiscountType), () => _discountTypes.DeleteAsync(id));
    }
    #endregion

    #region MovieType
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<MovieTypeDTO>), 200)]
    public Task<IActionResult> GetMovieTypes([FromBody] PagingSearchDTO search)
    {
        return Run(nameof(GetMovieTypes), () => _movieTypes.GetAsync(search));
    }

    [HttpGet]
    [ProducesResponseType(typeof(MovieTypeDTO), 200)]
    public async Task<IActionResult> GetMovieType([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _movieTypes.ExistsAsync(id), nameof(GetMovieType), nameof(MovieType), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(GetMovieType), () => _movieTypes.GetByIdAsync(id));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(MovieTypeDTO), 200)]
    public Task<IActionResult> CreateMovieType([FromBody] CreateMovieTypeRequest request)
    {
        return Run(nameof(CreateMovieType), () => _movieTypes.CreateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPut]
    [ProducesResponseType(typeof(MovieTypeDTO), 200)]
    public async Task<IActionResult> UpdateMovieType([FromBody] UpdateMovieTypeRequest request)
    {
        var notFound = await EnsureExistsAsync(() => _movieTypes.ExistsAsync(request.Id), nameof(UpdateMovieType), nameof(MovieType), request.Id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(UpdateMovieType), () => _movieTypes.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteMovieType([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _movieTypes.ExistsAsync(id), nameof(DeleteMovieType), nameof(MovieType), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await RunNoContent(nameof(DeleteMovieType), () => _movieTypes.DeleteAsync(id));
    }
    #endregion

    #region SeatType
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<SeatTypeDTO>), 200)]
    public Task<IActionResult> GetSeatTypes([FromBody] PagingSearchDTO search)
    {
        return Run(nameof(GetSeatTypes), () => _seatTypes.GetAsync(search));
    }

    [HttpGet]
    [ProducesResponseType(typeof(SeatTypeDTO), 200)]
    public async Task<IActionResult> GetSeatType([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _seatTypes.ExistsAsync(id), nameof(GetSeatType), nameof(SeatType), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(GetSeatType), () => _seatTypes.GetByIdAsync(id));
    }

    // SeatType has no create/delete action — a theater always has exactly Standard + Double, seeded on
    // theater creation (ISeatTypeManager.EnsureDefaultsAsync). Only Name/Description/Color are editable.
    [Authorize(Roles = _adminRole)]
    [HttpPut]
    [ProducesResponseType(typeof(SeatTypeDTO), 200)]
    public async Task<IActionResult> UpdateSeatType([FromBody] UpdateSeatTypeRequest request)
    {
        var notFound = await EnsureExistsAsync(() => _seatTypes.ExistsAsync(request.Id), nameof(UpdateSeatType), nameof(SeatType), request.Id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(UpdateSeatType), () => _seatTypes.UpdateAsync(request));
    }
    #endregion

    #region PatronCategory
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<PatronCategoryDTO>), 200)]
    public Task<IActionResult> GetPatronCategories([FromBody] PagingSearchDTO search)
    {
        return Run(nameof(GetPatronCategories), () => _patronCategories.GetAsync(search));
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<PatronCategoryDTO>), 200)]
    public Task<IActionResult> GetPatronCategoriesByTheater([FromQuery] Guid theaterId)
    {
        return Run(nameof(GetPatronCategoriesByTheater), () => _patronCategories.GetByTheaterAsync(theaterId));
    }

    [HttpGet]
    [ProducesResponseType(typeof(PatronCategoryDTO), 200)]
    public async Task<IActionResult> GetPatronCategory([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _patronCategories.ExistsAsync(id), nameof(GetPatronCategory), nameof(PatronCategory), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(GetPatronCategory), () => _patronCategories.GetByIdAsync(id));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(PatronCategoryDTO), 200)]
    public Task<IActionResult> CreatePatronCategory([FromBody] CreatePatronCategoryRequest request)
    {
        return Run(nameof(CreatePatronCategory), () => _patronCategories.CreateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPut]
    [ProducesResponseType(typeof(PatronCategoryDTO), 200)]
    public async Task<IActionResult> UpdatePatronCategory([FromBody] UpdatePatronCategoryRequest request)
    {
        var notFound = await EnsureExistsAsync(() => _patronCategories.ExistsAsync(request.Id), nameof(UpdatePatronCategory), nameof(PatronCategory), request.Id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(UpdatePatronCategory), () => _patronCategories.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeletePatronCategory([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _patronCategories.ExistsAsync(id), nameof(DeletePatronCategory), nameof(PatronCategory), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await RunNoContent(nameof(DeletePatronCategory), () => _patronCategories.DeleteAsync(id));
    }
    #endregion

    #region TimeSlot
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<TimeSlotDTO>), 200)]
    public Task<IActionResult> GetTimeSlots([FromBody] PagingSearchDTO search)
    {
        return Run(nameof(GetTimeSlots), () => _timeSlots.GetAsync(search));
    }

    [HttpGet]
    [ProducesResponseType(typeof(TimeSlotDTO), 200)]
    public async Task<IActionResult> GetTimeSlot([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _timeSlots.ExistsAsync(id), nameof(GetTimeSlot), nameof(TimeSlot), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(GetTimeSlot), () => _timeSlots.GetByIdAsync(id));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(TimeSlotDTO), 200)]
    public Task<IActionResult> CreateTimeSlot([FromBody] CreateTimeSlotRequest request)
    {
        return Run(nameof(CreateTimeSlot), () => _timeSlots.CreateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPut]
    [ProducesResponseType(typeof(TimeSlotDTO), 200)]
    public async Task<IActionResult> UpdateTimeSlot([FromBody] UpdateTimeSlotRequest request)
    {
        var notFound = await EnsureExistsAsync(() => _timeSlots.ExistsAsync(request.Id), nameof(UpdateTimeSlot), nameof(TimeSlot), request.Id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(UpdateTimeSlot), () => _timeSlots.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteTimeSlot([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _timeSlots.ExistsAsync(id), nameof(DeleteTimeSlot), nameof(TimeSlot), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await RunNoContent(nameof(DeleteTimeSlot), () => _timeSlots.DeleteAsync(id));
    }
    #endregion

    #region TicketPrice
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<TicketPriceDTO>), 200)]
    public Task<IActionResult> GetTicketPrices([FromBody] PagingSearchDTO search)
    {
        return Run(nameof(GetTicketPrices), () => _ticketPrices.GetAsync(search));
    }

    [HttpGet]
    [ProducesResponseType(typeof(TicketPriceDTO), 200)]
    public async Task<IActionResult> GetTicketPrice([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _ticketPrices.ExistsAsync(id), nameof(GetTicketPrice), nameof(TicketPrice), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(GetTicketPrice), () => _ticketPrices.GetByIdAsync(id));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(TicketPriceDTO), 200)]
    public Task<IActionResult> CreateTicketPrice([FromBody] CreateTicketPriceRequest request)
    {
        return Run(nameof(CreateTicketPrice), () => _ticketPrices.CreateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPut]
    [ProducesResponseType(typeof(TicketPriceDTO), 200)]
    public async Task<IActionResult> UpdateTicketPrice([FromBody] UpdateTicketPriceRequest request)
    {
        var notFound = await EnsureExistsAsync(() => _ticketPrices.ExistsAsync(request.Id), nameof(UpdateTicketPrice), nameof(TicketPrice), request.Id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(UpdateTicketPrice), () => _ticketPrices.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteTicketPrice([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _ticketPrices.ExistsAsync(id), nameof(DeleteTicketPrice), nameof(TicketPrice), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await RunNoContent(nameof(DeleteTicketPrice), () => _ticketPrices.DeleteAsync(id));
    }
    #endregion


    #region UserType
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<UserTypeDTO>), 200)]
    public Task<IActionResult> GetUserTypes([FromBody] PagingSearchDTO search)
    {
        return Run(nameof(GetUserTypes), () => _userTypes.GetAsync(search));
    }

    [HttpGet]
    [ProducesResponseType(typeof(UserTypeDTO), 200)]
    public async Task<IActionResult> GetUserType([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _userTypes.ExistsAsync(id), nameof(GetUserType), nameof(UserType), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(GetUserType), () => _userTypes.GetByIdAsync(id));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(UserTypeDTO), 200)]
    public Task<IActionResult> CreateUserType([FromBody] CreateUserTypeRequest request)
    {
        return Run(nameof(CreateUserType), () => _userTypes.CreateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPut]
    [ProducesResponseType(typeof(UserTypeDTO), 200)]
    public async Task<IActionResult> UpdateUserType([FromBody] UpdateUserTypeRequest request)
    {
        var notFound = await EnsureExistsAsync(() => _userTypes.ExistsAsync(request.Id), nameof(UpdateUserType), nameof(UserType), request.Id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(UpdateUserType), () => _userTypes.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteUserType([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _userTypes.ExistsAsync(id), nameof(DeleteUserType), nameof(UserType), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await RunNoContent(nameof(DeleteUserType), () => _userTypes.DeleteAsync(id));
    }
    #endregion

    #region MemberShip
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<MemberShipDTO>), 200)]
    public Task<IActionResult> GetMemberShips([FromBody] PagingSearchDTO search)
    {
        return Run(nameof(GetMemberShips), () => _memberShips.GetAsync(search));
    }

    [HttpGet]
    [ProducesResponseType(typeof(MemberShipDTO), 200)]
    public async Task<IActionResult> GetMemberShip([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _memberShips.ExistsAsync(id), nameof(GetMemberShip), nameof(MemberShip), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(GetMemberShip), () => _memberShips.GetByIdAsync(id));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(MemberShipDTO), 200)]
    public Task<IActionResult> CreateMemberShip([FromBody] CreateMemberShipRequest request)
    {
        return Run(nameof(CreateMemberShip), () => _memberShips.CreateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPut]
    [ProducesResponseType(typeof(MemberShipDTO), 200)]
    public async Task<IActionResult> UpdateMemberShip([FromBody] UpdateMemberShipRequest request)
    {
        var notFound = await EnsureExistsAsync(() => _memberShips.ExistsAsync(request.Id), nameof(UpdateMemberShip), nameof(MemberShip), request.Id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(UpdateMemberShip), () => _memberShips.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteMemberShip([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _memberShips.ExistsAsync(id), nameof(DeleteMemberShip), nameof(MemberShip), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await RunNoContent(nameof(DeleteMemberShip), () => _memberShips.DeleteAsync(id));
    }
    #endregion

    #region Holiday
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<HolidayDTO>), 200)]
    public Task<IActionResult> GetHolidays([FromBody] PagingSearchDTO search)
    {
        return Run(nameof(GetHolidays), () => _holidays.GetAsync(search));
    }

    [HttpGet]
    [ProducesResponseType(typeof(HolidayDTO), 200)]
    public async Task<IActionResult> GetHoliday([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _holidays.ExistsAsync(id), nameof(GetHoliday), nameof(Holiday), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(GetHoliday), () => _holidays.GetByIdAsync(id));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(HolidayDTO), 200)]
    public Task<IActionResult> CreateHoliday([FromBody] CreateHolidayRequest request)
    {
        return Run(nameof(CreateHoliday), () => _holidays.CreateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPut]
    [ProducesResponseType(typeof(HolidayDTO), 200)]
    public async Task<IActionResult> UpdateHoliday([FromBody] UpdateHolidayRequest request)
    {
        var notFound = await EnsureExistsAsync(() => _holidays.ExistsAsync(request.Id), nameof(UpdateHoliday), nameof(Holiday), request.Id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(UpdateHoliday), () => _holidays.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteHoliday([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _holidays.ExistsAsync(id), nameof(DeleteHoliday), nameof(Holiday), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await RunNoContent(nameof(DeleteHoliday), () => _holidays.DeleteAsync(id));
    }
    #endregion

    #region News
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<NewsDTO>), 200)]
    public Task<IActionResult> GetNewsList([FromBody] PagingSearchDTO search)
    {
        return Run(nameof(GetNewsList), () => _news.GetAsync(search));
    }

    [HttpGet]
    [ProducesResponseType(typeof(NewsDTO), 200)]
    public async Task<IActionResult> GetNews([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _news.ExistsAsync(id), nameof(GetNews), nameof(News), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(GetNews), () => _news.GetByIdAsync(id));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(NewsDTO), 200)]
    public Task<IActionResult> CreateNews([FromBody] CreateNewsRequest request)
    {
        return Run(nameof(CreateNews), () => _news.CreateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPut]
    [ProducesResponseType(typeof(NewsDTO), 200)]
    public async Task<IActionResult> UpdateNews([FromBody] UpdateNewsRequest request)
    {
        var notFound = await EnsureExistsAsync(() => _news.ExistsAsync(request.Id), nameof(UpdateNews), nameof(News), request.Id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(UpdateNews), () => _news.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteNews([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _news.ExistsAsync(id), nameof(DeleteNews), nameof(News), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await RunNoContent(nameof(DeleteNews), () => _news.DeleteAsync(id));
    }
    #endregion

    #region Discount
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<DiscountDTO>), 200)]
    public Task<IActionResult> GetDiscounts([FromBody] PagingSearchDTO search)
    {
        return Run(nameof(GetDiscounts), () => _discounts.GetAsync(search));
    }

    [HttpGet]
    [ProducesResponseType(typeof(DiscountDTO), 200)]
    public async Task<IActionResult> GetDiscount([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _discounts.ExistsAsync(id), nameof(GetDiscount), nameof(Discount), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(GetDiscount), () => _discounts.GetByIdAsync(id));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(DiscountDTO), 200)]
    public Task<IActionResult> CreateDiscount([FromBody] CreateDiscountRequest request)
    {
        return Run(nameof(CreateDiscount), () => _discounts.CreateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPut]
    [ProducesResponseType(typeof(DiscountDTO), 200)]
    public async Task<IActionResult> UpdateDiscount([FromBody] UpdateDiscountRequest request)
    {
        var notFound = await EnsureExistsAsync(() => _discounts.ExistsAsync(request.Id), nameof(UpdateDiscount), nameof(Discount), request.Id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(UpdateDiscount), () => _discounts.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteDiscount([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _discounts.ExistsAsync(id), nameof(DeleteDiscount), nameof(Discount), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await RunNoContent(nameof(DeleteDiscount), () => _discounts.DeleteAsync(id));
    }
    #endregion

    #region FoodAndDrink
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<FoodAndDrinkDTO>), 200)]
    public Task<IActionResult> GetFoodAndDrinks([FromBody] PagingSearchDTO search)
    {
        return Run(nameof(GetFoodAndDrinks), () => _foodAndDrinks.GetAsync(search));
    }

    [HttpGet]
    [ProducesResponseType(typeof(FoodAndDrinkDTO), 200)]
    public async Task<IActionResult> GetFoodAndDrink([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _foodAndDrinks.ExistsAsync(id), nameof(GetFoodAndDrink), nameof(FoodAndDrink), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(GetFoodAndDrink), () => _foodAndDrinks.GetByIdAsync(id));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(FoodAndDrinkDTO), 200)]
    public Task<IActionResult> CreateFoodAndDrink([FromBody] CreateFoodAndDrinkRequest request)
    {
        return Run(nameof(CreateFoodAndDrink), () => _foodAndDrinks.CreateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPut]
    [ProducesResponseType(typeof(FoodAndDrinkDTO), 200)]
    public async Task<IActionResult> UpdateFoodAndDrink([FromBody] UpdateFoodAndDrinkRequest request)
    {
        var notFound = await EnsureExistsAsync(() => _foodAndDrinks.ExistsAsync(request.Id), nameof(UpdateFoodAndDrink), nameof(FoodAndDrink), request.Id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(UpdateFoodAndDrink), () => _foodAndDrinks.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteFoodAndDrink([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _foodAndDrinks.ExistsAsync(id), nameof(DeleteFoodAndDrink), nameof(FoodAndDrink), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await RunNoContent(nameof(DeleteFoodAndDrink), () => _foodAndDrinks.DeleteAsync(id));
    }
    #endregion

    #region Room
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<RoomDTO>), 200)]
    public Task<IActionResult> GetRooms([FromBody] PagingSearchDTO search)
    {
        return Run(nameof(GetRooms), () => _rooms.GetAsync(search));
    }

    [HttpGet]
    [ProducesResponseType(typeof(RoomDTO), 200)]
    public async Task<IActionResult> GetRoom([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _rooms.ExistsAsync(id), nameof(GetRoom), nameof(Room), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(GetRoom), () => _rooms.GetByIdAsync(id));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(RoomDTO), 200)]
    public Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request)
    {
        return Run(nameof(CreateRoom), () => _rooms.CreateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPut]
    [ProducesResponseType(typeof(RoomDTO), 200)]
    public async Task<IActionResult> UpdateRoom([FromBody] UpdateRoomRequest request)
    {
        var notFound = await EnsureExistsAsync(() => _rooms.ExistsAsync(request.Id), nameof(UpdateRoom), nameof(Room), request.Id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(UpdateRoom), () => _rooms.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteRoom([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _rooms.ExistsAsync(id), nameof(DeleteRoom), nameof(Room), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await RunNoContent(nameof(DeleteRoom), () => _rooms.DeleteAsync(id));
    }
    #endregion

    #region RoomType
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<RoomTypeDTO>), 200)]
    public Task<IActionResult> GetRoomTypes([FromBody] PagingSearchDTO search)
    {
        return Run(nameof(GetRoomTypes), () => _roomTypes.GetAsync(search));
    }

    [HttpGet]
    [ProducesResponseType(typeof(RoomTypeDTO), 200)]
    public async Task<IActionResult> GetRoomType([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _roomTypes.ExistsAsync(id), nameof(GetRoomType), nameof(RoomType), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(GetRoomType), () => _roomTypes.GetByIdAsync(id));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(RoomTypeDTO), 200)]
    public Task<IActionResult> CreateRoomType([FromBody] CreateRoomTypeRequest request)
    {
        return Run(nameof(CreateRoomType), () => _roomTypes.CreateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPut]
    [ProducesResponseType(typeof(RoomTypeDTO), 200)]
    public async Task<IActionResult> UpdateRoomType([FromBody] UpdateRoomTypeRequest request)
    {
        var notFound = await EnsureExistsAsync(() => _roomTypes.ExistsAsync(request.Id), nameof(UpdateRoomType), nameof(RoomType), request.Id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(UpdateRoomType), () => _roomTypes.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteRoomType([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _roomTypes.ExistsAsync(id), nameof(DeleteRoomType), nameof(RoomType), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await RunNoContent(nameof(DeleteRoomType), () => _roomTypes.DeleteAsync(id));
    }

    [Authorize(Roles = _adminRole)]
    [HttpGet]
    [ProducesResponseType(typeof(List<RoomTypePatronCategoryPriceDTO>), 200)]
    public Task<IActionResult> GetRoomTypePatronCategoryPrices([FromQuery] Guid roomTypeId)
    {
        return Run(nameof(GetRoomTypePatronCategoryPrices), () => _roomTypePatronCategoryPrices.GetByRoomTypeAsync(roomTypeId));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(204)]
    public Task<IActionResult> SaveRoomTypePatronCategoryPrices([FromBody] SaveRoomTypePatronCategoryPricesRequest request)
    {
        return RunNoContent(nameof(SaveRoomTypePatronCategoryPrices), () => _roomTypePatronCategoryPrices.SaveAsync(request));
    }
    #endregion

    #region ShowTime
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<ShowTimeDTO>), 200)]
    public Task<IActionResult> GetShowTimeList([FromBody] PagingSearchDTO search)
    {
        return Run(nameof(GetShowTimeList), () => _showTimes.GetAsync(search));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ShowTimeDTO), 200)]
    public async Task<IActionResult> GetShowTime([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _showTimes.ExistsAsync(id), nameof(GetShowTime), nameof(ShowTime), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(GetShowTime), () => _showTimes.GetByIdAsync(id));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(ShowTimeDTO), 200)]
    public Task<IActionResult> CreateShowTime([FromBody] CreateShowTimeRequest request)
    {
        return Run(nameof(CreateShowTime), () => _showTimes.CreateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPut]
    [ProducesResponseType(typeof(ShowTimeDTO), 200)]
    public async Task<IActionResult> UpdateShowTime([FromBody] UpdateShowTimeRequest request)
    {
        var notFound = await EnsureExistsAsync(() => _showTimes.ExistsAsync(request.Id), nameof(UpdateShowTime), nameof(ShowTime), request.Id);
        if (notFound != null)
        {
            return notFound;
        }
        return await Run(nameof(UpdateShowTime), () => _showTimes.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteShowTime([FromQuery] Guid id)
    {
        var notFound = await EnsureExistsAsync(() => _showTimes.ExistsAsync(id), nameof(DeleteShowTime), nameof(ShowTime), id);
        if (notFound != null)
        {
            return notFound;
        }
        return await RunNoContent(nameof(DeleteShowTime), () => _showTimes.DeleteAsync(id));
    }
    #endregion

    #region MovieTypeDetail (Movie ↔ MovieType)
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<MovieTypeDetailDTO>), 200)]
    public Task<IActionResult> GetMovieTypeDetails([FromBody] PagingSearchDTO search)
    {
        return Run(nameof(GetMovieTypeDetails), () => _movieTypeDetails.GetAsync(search));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(MovieTypeDetailDTO), 200)]
    public Task<IActionResult> CreateMovieTypeDetail([FromBody] CreateMovieTypeDetailRequest request)
    {
        return Run(nameof(CreateMovieTypeDetail), () => _movieTypeDetails.CreateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public Task<IActionResult> DeleteMovieTypeDetail([FromQuery] Guid movieId, [FromQuery] Guid movieTypeId)
    {
        return RunNoContent(nameof(DeleteMovieTypeDetail), () => _movieTypeDetails.DeleteAsync(movieId, movieTypeId));
    }
    #endregion

    #region Room seat map (seat-type assignment + double-seat grouping)
    // Read-only layout: the staff app's seat picker (block a seat, report an incident) needs it for non-admin roles too.
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpGet]
    [ProducesResponseType(typeof(List<RoomSeatDTO>), 200)]
    public Task<IActionResult> GetRoomSeatMap([FromQuery] Guid roomId)
    {
        return Run(nameof(GetRoomSeatMap), () => _rooms.GetSeatMapAsync(roomId));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(204)]
    public Task<IActionResult> SaveRoomSeatMap([FromBody] SaveSeatMapRequest request)
    {
        return RunNoContent(nameof(SaveRoomSeatMap), () => _rooms.SaveSeatMapAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(List<RoomSeatDTO>), 200)]
    public Task<IActionResult> ResizeRoomSeatGrid([FromBody] ResizeSeatGridRequest request)
    {
        return Run(nameof(ResizeRoomSeatGrid), () => _rooms.ResizeSeatGridAsync(request));
    }
    #endregion

    #region Invoice (admin: list / status / delete)
    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<InvoiceAdminDTO>), 200)]
    public Task<IActionResult> GetInvoices([FromBody] PagingSearchDTO search)
    {
        return Run(nameof(GetInvoices), () => _invoices.GetAsync(search));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPut]
    [ProducesResponseType(typeof(InvoiceAdminDTO), 200)]
    public Task<IActionResult> UpdateInvoiceStatus([FromBody] UpdateInvoiceStatusRequest request)
    {
        return Run(nameof(UpdateInvoiceStatus), () => _invoices.UpdateStatusAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public Task<IActionResult> DeleteInvoice([FromQuery] Guid id)
    {
        return RunNoContent(nameof(DeleteInvoice), () => _invoices.DeleteAsync(id));
    }
    #endregion

    #region Catalog helpers (shared try/catch + logging wrappers)
    private async Task<IActionResult> Run<T>(string action, Func<Task<T>> op)
    {
        LogProvider.Current.Information($"{GetType().Name}.{action} being awakened to process request...");
        try
        {
            return Ok(await op());
        }
        catch (Exception e)
        {
            return HandleException(e, action);
        }
    }

    private async Task<IActionResult> RunNoContent(string action, Func<Task> op)
    {
        LogProvider.Current.Information($"{GetType().Name}.{action} being awakened to process request...");
        try
        {
            await op();
            return NoContent();
        }
        catch (Exception e)
        {
            return HandleException(e, action);
        }
    }
    #endregion

    #region Combo
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(List<ComboComponentDTO>), 200)]
    public Task<IActionResult> GetComboComponents([FromQuery] Guid comboId)
    {
        return Run(nameof(GetComboComponents), () => _combos.GetComponentsAsync(comboId));
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost]
    [ProducesResponseType(204)]
    public Task<IActionResult> SaveComboComposition([FromBody] SaveComboRequest request)
    {
        return RunNoContent(nameof(SaveComboComposition), () => _combos.SaveAsync(request));
    }
    #endregion

    #region Inventory
    [Authorize(Roles = RoleNames.BackOffice)]
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<InventoryItemDTO>), 200)]
    public Task<IActionResult> GetInventory([FromBody] PagingSearchDTO search, [FromServices] IInventoryManager inventory)
    {
        if (!User.TryGetBackOfficeScope(out var scope))
        {
            return Task.FromResult<IActionResult>(Forbid());
        }
        return Run(nameof(GetInventory), () => inventory.GetInventoryAsync(search, scope));
    }

    [Authorize(Roles = RoleNames.StockApprovers)]
    [HttpPost]
    [ProducesResponseType(typeof(InventoryItemDTO), 200)]
    public Task<IActionResult> UpdateInventorySettings([FromBody] UpdateInventorySettingsRequest request, [FromServices] IInventoryManager inventory)
    {
        if (!User.TryGetBackOfficeScope(out var scope))
        {
            return Task.FromResult<IActionResult>(Forbid());
        }
        return Run(nameof(UpdateInventorySettings), () => inventory.UpdateSettingsAsync(request, User.GetUserId(), scope));
    }

    [Authorize(Roles = RoleNames.BackOffice)]
    [HttpPost]
    [ProducesResponseType(typeof(InventoryItemDTO), 200)]
    public Task<IActionResult> RecordStockMovement([FromBody] RecordStockMovementRequest request, [FromServices] IInventoryManager inventory)
    {
        if (!User.TryGetBackOfficeScope(out var scope))
        {
            return Task.FromResult<IActionResult>(Forbid());
        }
        return Run(nameof(RecordStockMovement), () => inventory.RecordMovementAsync(request, User.GetUserId(), scope));
    }

    [Authorize(Roles = RoleNames.BackOffice)]
    [HttpPost]
    [ProducesResponseType(typeof(InventoryItemDTO), 200)]
    public Task<IActionResult> RecordStockCount([FromBody] RecordStockCountRequest request, [FromServices] IInventoryManager inventory)
    {
        if (!User.TryGetBackOfficeScope(out var scope))
        {
            return Task.FromResult<IActionResult>(Forbid());
        }
        return Run(nameof(RecordStockCount), () => inventory.RecordStockCountAsync(request, User.GetUserId(), scope));
    }

    [Authorize(Roles = RoleNames.BackOffice)]
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<StockMovementDTO>), 200)]
    public Task<IActionResult> GetStockMovements([FromBody] PagingSearchDTO search, [FromServices] IInventoryManager inventory)
    {
        if (!User.TryGetBackOfficeScope(out var scope))
        {
            return Task.FromResult<IActionResult>(Forbid());
        }
        return Run(nameof(GetStockMovements), () => inventory.GetMovementsAsync(search, scope));
    }
    #endregion

    #region StoragePlan
    [Authorize(Roles = RoleNames.BackOffice)]
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<StoragePlanListItemDTO>), 200)]
    public Task<IActionResult> GetStoragePlans([FromBody] PagingSearchDTO search, [FromServices] IStoragePlanManager plans)
    {
        if (!User.TryGetBackOfficeScope(out var scope))
        {
            return Task.FromResult<IActionResult>(Forbid());
        }
        return Run(nameof(GetStoragePlans), () => plans.GetAsync(search, scope));
    }

    [Authorize(Roles = RoleNames.BackOffice)]
    [HttpGet]
    [ProducesResponseType(typeof(StoragePlanDTO), 200)]
    public Task<IActionResult> GetStoragePlan([FromQuery] Guid id, [FromServices] IStoragePlanManager plans)
    {
        if (!User.TryGetBackOfficeScope(out var scope))
        {
            return Task.FromResult<IActionResult>(Forbid());
        }
        return Run(nameof(GetStoragePlan), () => plans.GetByIdAsync(id, scope));
    }

    [Authorize(Roles = RoleNames.BackOffice)]
    [HttpPost]
    [ProducesResponseType(typeof(StoragePlanDTO), 200)]
    public Task<IActionResult> CreateStoragePlan([FromBody] SaveStoragePlanRequest request, [FromServices] IStoragePlanManager plans)
    {
        if (!User.TryGetBackOfficeScope(out var scope))
        {
            return Task.FromResult<IActionResult>(Forbid());
        }
        return Run(nameof(CreateStoragePlan), () => plans.CreateAsync(request, User.GetUserId(), scope));
    }

    [Authorize(Roles = RoleNames.BackOffice)]
    [HttpPut]
    [ProducesResponseType(typeof(StoragePlanDTO), 200)]
    public Task<IActionResult> UpdateStoragePlan([FromBody] SaveStoragePlanRequest request, [FromServices] IStoragePlanManager plans)
    {
        if (!User.TryGetBackOfficeScope(out var scope))
        {
            return Task.FromResult<IActionResult>(Forbid());
        }
        return Run(nameof(UpdateStoragePlan), () => plans.UpdateAsync(request, User.GetUserId(), scope));
    }

    [Authorize(Roles = RoleNames.BackOffice)]
    [HttpPost]
    [ProducesResponseType(typeof(StoragePlanDTO), 200)]
    public Task<IActionResult> SubmitStoragePlan([FromBody] StoragePlanDecisionRequest request, [FromServices] IStoragePlanManager plans)
    {
        if (!User.TryGetBackOfficeScope(out var scope))
        {
            return Task.FromResult<IActionResult>(Forbid());
        }
        return Run(nameof(SubmitStoragePlan), () => plans.SubmitAsync(request.Id, User.GetUserId(), scope));
    }

    [Authorize(Roles = RoleNames.StockApprovers)]
    [HttpPost]
    [ProducesResponseType(typeof(StoragePlanDTO), 200)]
    public Task<IActionResult> ApproveStoragePlan([FromBody] StoragePlanDecisionRequest request, [FromServices] IStoragePlanManager plans)
    {
        if (!User.TryGetBackOfficeScope(out var scope))
        {
            return Task.FromResult<IActionResult>(Forbid());
        }
        return Run(nameof(ApproveStoragePlan), () => plans.ApproveAsync(request.Id, User.GetUserId(), scope));
    }

    [Authorize(Roles = RoleNames.StockApprovers)]
    [HttpPost]
    [ProducesResponseType(typeof(StoragePlanDTO), 200)]
    public Task<IActionResult> RejectStoragePlan([FromBody] StoragePlanDecisionRequest request, [FromServices] IStoragePlanManager plans)
    {
        if (!User.TryGetBackOfficeScope(out var scope))
        {
            return Task.FromResult<IActionResult>(Forbid());
        }
        return Run(nameof(RejectStoragePlan), () => plans.RejectAsync(request.Id, request.Reason, User.GetUserId(), scope));
    }

    [Authorize(Roles = RoleNames.BackOffice)]
    [HttpPost]
    [ProducesResponseType(typeof(StoragePlanDTO), 200)]
    public Task<IActionResult> ReceiveStoragePlan([FromBody] ReceiveStoragePlanRequest request, [FromServices] IStoragePlanManager plans)
    {
        if (!User.TryGetBackOfficeScope(out var scope))
        {
            return Task.FromResult<IActionResult>(Forbid());
        }
        return Run(nameof(ReceiveStoragePlan), () => plans.ReceiveAsync(request, User.GetUserId(), scope));
    }

    [Authorize(Roles = RoleNames.BackOffice)]
    [HttpPost]
    [ProducesResponseType(typeof(StoragePlanDTO), 200)]
    public Task<IActionResult> CancelStoragePlan([FromBody] StoragePlanDecisionRequest request, [FromServices] IStoragePlanManager plans)
    {
        if (!User.TryGetBackOfficeScope(out var scope))
        {
            return Task.FromResult<IActionResult>(Forbid());
        }
        return Run(nameof(CancelStoragePlan), () => plans.CancelAsync(request.Id, User.GetUserId(), scope));
    }

    [Authorize(Roles = RoleNames.BackOffice)]
    [HttpPost]
    [ProducesResponseType(typeof(StoragePlanDTO), 200)]
    public Task<IActionResult> CreateStoragePlanFromLowStock([FromBody] CreatePlanFromLowStockRequest request, [FromServices] IStoragePlanManager plans)
    {
        if (!User.TryGetBackOfficeScope(out var scope))
        {
            return Task.FromResult<IActionResult>(Forbid());
        }
        return Run(nameof(CreateStoragePlanFromLowStock), () => plans.CreateFromLowStockAsync(request, User.GetUserId(), scope));
    }
    #endregion

    // ════════════════════════════════════════════════════════════════════════════
    //  Staff
    // ════════════════════════════════════════════════════════════════════════════

    #region Concession

    /// <summary>Paid orders waiting to be prepared or handed over, by earliest showtime. Day null = today.</summary>
    [Authorize(Roles = RoleNames.Concession)]
    [HttpPost]
    [ProducesResponseType(typeof(List<PickupOrderDTO>), 200)]
    public async Task<IActionResult> GetPickupQueue([FromBody] GetPickupQueueRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetPickupQueue)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _concessions.GetPickupQueueAsync(theaterId, request.Day));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetPickupQueue));
        }
    }

    /// <summary>Moves an order one step (Preparing, Ready, HandedOver). An illegal move is 400.</summary>
    [Authorize(Roles = RoleNames.Concession)]
    [HttpPost]
    [ProducesResponseType(typeof(PickupOrderDTO), 200)]
    public async Task<IActionResult> SetFoodStatus([FromBody] SetFoodStatusRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(SetFoodStatus)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _concessions.SetFoodStatusAsync(theaterId, User.GetUserId(), request.InvoiceId, request.Status));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(SetFoodStatus));
        }
    }

    /// <summary>Tracked items at or under their low-stock threshold.</summary>
    [Authorize(Roles = RoleNames.Concession)]
    [HttpPost]
    [ProducesResponseType(typeof(List<LowStockItemDTO>), 200)]
    public async Task<IActionResult> GetLowStock([FromBody] GetLowStockRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetLowStock)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _concessions.GetLowStockAsync(theaterId));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetLowStock));
        }
    }

    /// <summary>Finds an order by the invoice code the customer shows (404 when unknown or another theater's).</summary>
    [Authorize(Roles = RoleNames.Concession)]
    [HttpPost]
    [ProducesResponseType(typeof(PickupOrderDTO), 200)]
    public async Task<IActionResult> LookupPickup([FromBody] LookupPickupRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(LookupPickup)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _concessions.LookupPickupAsync(theaterId, request.Code));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(LookupPickup));
        }
    }
    #endregion

    #region Gate

    /// <summary>
    /// Scans a ticket QR code. A refused scan is still HTTP 200 with the reason in <c>Outcome</c>, so the client's
    /// error interceptor stays quiet; only authorization problems are 403.
    /// </summary>
    [Authorize(Roles = RoleNames.GateKeepers)]
    [HttpPost]
    [ProducesResponseType(typeof(ScanTicketResultDTO), 200)]
    public async Task<IActionResult> Scan([FromBody] ScanTicketRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(Scan)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }

            return Ok(await _gate.ScanAsync(scope.Resolve(request.TheaterId), User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(Scan));
        }
    }

    /// <summary>Finds today's paid tickets of the theater by invoice code or phone (at least one required).</summary>
    [Authorize(Roles = RoleNames.GateKeepers)]
    [HttpPost]
    [ProducesResponseType(typeof(List<GateLookupResultDTO>), 200)]
    public async Task<IActionResult> Lookup([FromBody] GateLookupRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(Lookup)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }

            return Ok(await _gate.LookupAsync(scope.Resolve(request.TheaterId), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(Lookup));
        }
    }
    #endregion

    #region BoxOffice

    /// <summary>Prices a counter sale (seats and/or food) without changing anything.</summary>
    [Authorize(Roles = RoleNames.Sellers)]
    [HttpPost]
    [ProducesResponseType(typeof(CounterQuoteDTO), 200)]
    public async Task<IActionResult> Quote([FromBody] CounterSaleRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(Quote)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.QuoteAsync(theaterId, request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(Quote));
        }
    }

    /// <summary>Rings up a counter sale: a Paid invoice with its tenders, never a Pending one.</summary>
    [Authorize(Roles = RoleNames.Sellers)]
    [HttpPost]
    [ProducesResponseType(typeof(CounterSaleResultDTO), 200)]
    public async Task<IActionResult> Sell([FromBody] CounterSaleRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(Sell)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.SellAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(Sell));
        }
    }

    [Authorize(Roles = RoleNames.Sellers)]
    [HttpPost]
    [ProducesResponseType(typeof(CashDrawerDTO), 200)]
    public async Task<IActionResult> OpenDrawer([FromBody] OpenDrawerRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(OpenDrawer)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.OpenDrawerAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(OpenDrawer));
        }
    }

    /// <summary>The caller's open drawer with totals; <c>IsOpen = false</c> when there is none.</summary>
    [Authorize(Roles = RoleNames.Sellers)]
    [HttpPost]
    [ProducesResponseType(typeof(CashDrawerDTO), 200)]
    public async Task<IActionResult> GetMyDrawer([FromBody] BoxOfficeScopeRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetMyDrawer)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.GetMyDrawerAsync(theaterId, User.GetUserId()));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetMyDrawer));
        }
    }

    /// <summary>Records cash put into or taken out of the drawer. A pay-out needs a manager override.</summary>
    [Authorize(Roles = RoleNames.Sellers)]
    [HttpPost]
    [ProducesResponseType(typeof(CashDrawerDTO), 200)]
    public async Task<IActionResult> PayInOut([FromBody] PayInOutRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(PayInOut)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.PayInOutAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(PayInOut));
        }
    }

    /// <summary>The theater's showtimes of a business day (default today), one row per showtime and room.</summary>
    [Authorize(Roles = RoleNames.Sellers)]
    [HttpPost]
    [ProducesResponseType(typeof(List<CounterShowtimeDTO>), 200)]
    public async Task<IActionResult> GetShowtimesToday([FromBody] ShowtimesTodayRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetShowtimesToday)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.GetShowtimesTodayAsync(theaterId, request.Date));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetShowtimesToday));
        }
    }

    /// <summary>Finds a member by exact phone number to attach to a sale (404 when none).</summary>
    [Authorize(Roles = RoleNames.Sellers)]
    [HttpPost]
    [ProducesResponseType(typeof(CounterCustomerDTO), 200)]
    public async Task<IActionResult> FindCustomer([FromBody] FindCustomerRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(FindCustomer)} being awakened to process request...");
        try
        {
            return Ok(await _boxOffice.FindCustomerAsync(request.Phone));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(FindCustomer));
        }
    }

    /// <summary>After-sales search by exact invoice code and/or customer phone, scoped to the theater.</summary>
    [Authorize(Roles = RoleNames.Sellers)]
    [HttpPost]
    [ProducesResponseType(typeof(List<AfterSalesInvoiceDTO>), 200)]
    public async Task<IActionResult> FindInvoice([FromBody] FindInvoiceRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(FindInvoice)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.FindInvoiceAsync(theaterId, request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(FindInvoice));
        }
    }

    /// <summary>Refunds a whole paid invoice. 403 without a manager approval (unless the caller is a manager).</summary>
    [Authorize(Roles = RoleNames.Sellers)]
    [HttpPost]
    [ProducesResponseType(typeof(StaffRefundResultDTO), 200)]
    public async Task<IActionResult> StaffRefund([FromBody] StaffRefundRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(StaffRefund)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.StaffRefundAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(StaffRefund));
        }
    }

    /// <summary>Replaces a counter invoice by a new sale in one transaction. 403 without a manager approval (unless the caller is a manager).</summary>
    [Authorize(Roles = RoleNames.Sellers)]
    [HttpPost]
    [ProducesResponseType(typeof(ExchangeResultDTO), 200)]
    public async Task<IActionResult> Exchange([FromBody] ExchangeRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(Exchange)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.ExchangeAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(Exchange));
        }
    }

    /// <summary>Returns the tickets of a paid invoice again (audited; a manager approves when a ticket was used).</summary>
    [Authorize(Roles = RoleNames.Sellers)]
    [HttpPost]
    [ProducesResponseType(typeof(ReprintResultDTO), 200)]
    public async Task<IActionResult> Reprint([FromBody] ReprintRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(Reprint)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.ReprintAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(Reprint));
        }
    }

    /// <summary>Closes the caller's own drawer with the counted cash; a variance beyond the tolerance needs reconciliation.</summary>
    [Authorize(Roles = RoleNames.Sellers)]
    [HttpPost]
    [ProducesResponseType(typeof(CloseDrawerResultDTO), 200)]
    public async Task<IActionResult> CloseDrawer([FromBody] CloseDrawerRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(CloseDrawer)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.CloseDrawerAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(CloseDrawer));
        }
    }

    /// <summary>A manager accepts the variance of a closed drawer.</summary>
    [Authorize(Roles = RoleNames.Sellers)]
    [Authorize(Roles = RoleNames.Approvers)]
    [HttpPost]
    [ProducesResponseType(typeof(CloseDrawerResultDTO), 200)]
    public async Task<IActionResult> ReconcileDrawer([FromBody] ReconcileDrawerRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(ReconcileDrawer)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _boxOffice.ReconcileDrawerAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(ReconcileDrawer));
        }
    }
    #endregion

    #region CustomerService

    /// <summary>Sellers plus the regional manager, who may follow up and approve compensation.</summary>
    private const string _complaintRoles = RoleNames.Sellers;

    /// <summary>Finds a member by email, phone or invoice code: masked contact, tier, points and the last 20 invoices of the caller's theaters.</summary>
    [Authorize(Roles = RoleNames.Sellers)]
    [HttpPost]
    [ProducesResponseType(typeof(CustomerLookupDTO), 200)]
    public async Task<IActionResult> LookupCustomer([FromBody] LookupCustomerRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(LookupCustomer)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _customerService.LookupCustomerAsync(scope.ToTheaterFilter(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(LookupCustomer));
        }
    }

    /// <summary>Sends the e-ticket of a paid invoice again by email or SMS (audited, 3 per hour per invoice).</summary>
    [Authorize(Roles = RoleNames.Sellers)]
    [HttpPost]
    [ProducesResponseType(typeof(ResendETicketResultDTO), 200)]
    public async Task<IActionResult> ResendETicket([FromBody] ResendETicketRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(ResendETicket)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _customerService.ResendETicketAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(ResendETicket));
        }
    }

    [Authorize(Roles = _complaintRoles)]
    [HttpPost]
    [ProducesResponseType(typeof(ComplaintDTO), 200)]
    public async Task<IActionResult> CreateComplaint([FromBody] CreateComplaintRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(CreateComplaint)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _customerService.CreateComplaintAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(CreateComplaint));
        }
    }

    /// <summary>Complaint page, newest first. Filters: status, category, assignedTo, customerId, invoiceId, theaterId (must be in scope, else 403).</summary>
    [Authorize(Roles = _complaintRoles)]
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<ComplaintDTO>), 200)]
    public async Task<IActionResult> GetComplaints([FromBody] PagingSearchDTO search)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetComplaints)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }

            IReadOnlyCollection<Guid>? theaterIds = scope.ToTheaterFilter();
            if (search?.Filters != null
                && search.Filters.TryGetValue(_theaterIdFilter, out var requested)
                && Guid.TryParse(requested, out var requestedTheaterId))
            {
                theaterIds = new[] { scope.Resolve(requestedTheaterId) };
            }

            return Ok(await _customerService.SearchComplaintsAsync(theaterIds, search ?? new PagingSearchDTO()));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetComplaints));
        }
    }

    [Authorize(Roles = _complaintRoles)]
    [HttpPost]
    [ProducesResponseType(typeof(ComplaintDTO), 200)]
    public async Task<IActionResult> GetComplaint([FromBody] GetComplaintRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetComplaint)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _customerService.GetComplaintAsync(scope.ToTheaterFilter(), request.ComplaintId));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetComplaint));
        }
    }

    [Authorize(Roles = _complaintRoles)]
    [HttpPost]
    [ProducesResponseType(typeof(ComplaintDTO), 200)]
    public async Task<IActionResult> UpdateComplaint([FromBody] UpdateComplaintRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(UpdateComplaint)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _customerService.UpdateComplaintAsync(scope.ToTheaterFilter(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(UpdateComplaint));
        }
    }

    /// <summary>Open to InReview, assigned to a staff member (default: the caller).</summary>
    [Authorize(Roles = _complaintRoles)]
    [HttpPost]
    [ProducesResponseType(typeof(ComplaintDTO), 200)]
    public async Task<IActionResult> StartComplaintReview([FromBody] StartComplaintReviewRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(StartComplaintReview)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _customerService.StartComplaintReviewAsync(scope.ToTheaterFilter(), User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(StartComplaintReview));
        }
    }

    [Authorize(Roles = _complaintRoles)]
    [HttpPost]
    [ProducesResponseType(typeof(ComplaintDTO), 200)]
    public async Task<IActionResult> RejectComplaint([FromBody] RejectComplaintRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(RejectComplaint)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _customerService.RejectComplaintAsync(scope.ToTheaterFilter(), User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(RejectComplaint));
        }
    }

    /// <summary>Resolves a complaint with Refund, GiftCard, Points or Apology. Compensation needs an approver or a manager override (else 403).</summary>
    [Authorize(Roles = _complaintRoles)]
    [HttpPost]
    [ProducesResponseType(typeof(ComplaintDTO), 200)]
    public async Task<IActionResult> ResolveComplaint([FromBody] ResolveComplaintRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(ResolveComplaint)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _customerService.ResolveComplaintAsync(scope.ToTheaterFilter(), User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(ResolveComplaint));
        }
    }
    #endregion

    #region StaffReport

    /// <summary>
    /// Audit trail, newest first. Filters: action, from, to, actorId, and optionally theaterId (must be inside the
    /// caller's scope, else 403). Admins see every theater; managers only their own.
    /// </summary>
    [Authorize(Roles = RoleNames.Reporting)]
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<AuditLogDTO>), 200)]
    public async Task<IActionResult> GetAuditLog([FromBody] PagingSearchDTO search)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetAuditLog)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }

            IReadOnlyCollection<Guid>? theaterIds = scope.ToTheaterFilter();
            if (search?.Filters != null
                && search.Filters.TryGetValue(_theaterIdFilter, out var requested)
                && Guid.TryParse(requested, out var requestedTheaterId))
            {
                theaterIds = new[] { scope.Resolve(requestedTheaterId) };
            }

            return Ok(await _reports.GetAuditLogAsync(search ?? new PagingSearchDTO(), theaterIds));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetAuditLog));
        }
    }

    // ── P9 reporting (append new actions below) ──────────────────────────────

    /// <summary>
    /// Sales grouped by Day, Movie, Theater, PaymentMethod, Staff or Channel: ticket and F&amp;B revenue separate, net of
    /// refunds. Range of business dates, at most 92 days. A theater outside the caller's scope is refused with 403.
    /// </summary>
    [Authorize(Roles = RoleNames.Reporting)]
    [HttpPost]
    [ProducesResponseType(typeof(SalesReportDTO), 200)]
    public async Task<IActionResult> GetSales([FromBody] StaffReportRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetSales)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _reports.GetSalesAsync(request, scope.ToTheaterFilter()));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetSales));
        }
    }

    /// <summary>Sold / active seats per screening starting in the range. Scope and range rules as for sales.</summary>
    [Authorize(Roles = RoleNames.Reporting)]
    [HttpPost]
    [ProducesResponseType(typeof(OccupancyReportDTO), 200)]
    public async Task<IActionResult> GetOccupancy([FromBody] StaffReportRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetOccupancy)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _reports.GetOccupancyAsync(request, scope.ToTheaterFilter()));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetOccupancy));
        }
    }

    /// <summary>Attach rate, refund rate (count and amount) and average spend per head. Scope and range rules as for sales.</summary>
    [Authorize(Roles = RoleNames.Reporting)]
    [HttpPost]
    [ProducesResponseType(typeof(StaffKpisDTO), 200)]
    public async Task<IActionResult> GetKpis([FromBody] StaffReportRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetKpis)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _reports.GetKpisAsync(request, scope.ToTheaterFilter()));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetKpis));
        }
    }

    // ── P5 after-sales ───────────────────────────────────────────────────────

    /// <summary>
    /// End-of-day cash close of one theater for a business day (Asia/Ho_Chi_Minh, 06:00 cut-off by default): totals by
    /// tender, refunds, exchanges, comps, tickets and food sold and the drawer sessions with their variance.
    /// </summary>
    [Authorize(Roles = RoleNames.Approvers)]
    [HttpPost]
    [ProducesResponseType(typeof(DailyCloseDTO), 200)]
    public async Task<IActionResult> GetDailyClose([FromBody] DailyCloseRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetDailyClose)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _dailyClose.GetDailyCloseAsync(theaterId, request.BusinessDate));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetDailyClose));
        }
    }
    #endregion

    #region Operations

    /// <summary>One theater's rooms with the day's showtimes: start, end, bufferEnd, movie, sold, capacity and room status.</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(ScheduleBoardDTO), 200)]
    public async Task<IActionResult> GetScheduleBoard([FromBody] ScheduleBoardRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetScheduleBoard)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _board.GetScheduleBoardAsync(theaterId, request.Date));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetScheduleBoard));
        }
    }

    /// <summary>Reports an incident (every staff role).</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(IncidentDTO), 200)]
    public async Task<IActionResult> ReportIncident([FromBody] ReportIncidentRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(ReportIncident)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _incidents.ReportAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(ReportIncident));
        }
    }

    /// <summary>Incident page, newest first. Filters: status, category, from, to, theaterId (must be in scope, else 403).</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<IncidentDTO>), 200)]
    public async Task<IActionResult> GetIncidents([FromBody] PagingSearchDTO search)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetIncidents)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }

            IReadOnlyCollection<Guid>? theaterIds = scope.ToTheaterFilter();
            if (search?.Filters != null
                && search.Filters.TryGetValue(_theaterIdFilter, out var requested)
                && Guid.TryParse(requested, out var requestedTheaterId))
            {
                theaterIds = new[] { scope.Resolve(requestedTheaterId) };
            }

            return Ok(await _incidents.SearchAsync(theaterIds, search ?? new PagingSearchDTO()));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetIncidents));
        }
    }

    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(IncidentDTO), 200)]
    public async Task<IActionResult> GetIncident([FromBody] GetIncidentRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetIncident)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _incidents.GetAsync(scope.ToTheaterFilter(), request.IncidentId));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetIncident));
        }
    }

    /// <summary>Closes an incident; with Unblock it also reopens the blocked seat/room (approver or manager override).</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(IncidentDTO), 200)]
    public async Task<IActionResult> ResolveIncident([FromBody] ResolveIncidentRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(ResolveIncident)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _incidents.ResolveAsync(scope.ToTheaterFilter(), User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(ResolveIncident));
        }
    }

    /// <summary>
    /// Takes a seat (and its double-seat partner) out of sale. Approvers, or any staff with a manager override (403
    /// without one). Returns the upcoming tickets on the seat; nothing is cancelled.
    /// </summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(BlockResultDTO), 200)]
    public async Task<IActionResult> BlockSeat([FromBody] BlockSeatRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(BlockSeat)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _incidents.BlockSeatAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(BlockSeat));
        }
    }

    /// <summary>
    /// Puts a room into maintenance. Approvers, or any staff with a manager override (403 without one). Returns the
    /// upcoming tickets in the room; nothing is cancelled.
    /// </summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(BlockResultDTO), 200)]
    public async Task<IActionResult> BlockRoom([FromBody] BlockRoomRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(BlockRoom)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _incidents.BlockRoomAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(BlockRoom));
        }
    }

    /// <summary>The theater's checklist templates (approvers manage them).</summary>
    [Authorize(Roles = RoleNames.Approvers)]
    [HttpPost]
    [ProducesResponseType(typeof(List<ChecklistTemplateDTO>), 200)]
    public async Task<IActionResult> GetChecklistTemplates([FromBody] GetChecklistTemplatesRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetChecklistTemplates)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _checklists.GetTemplatesAsync(theaterId));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetChecklistTemplates));
        }
    }

    /// <summary>Creates or edits a checklist template (approvers only; one active template per theater and kind).</summary>
    [Authorize(Roles = RoleNames.Approvers)]
    [HttpPost]
    [ProducesResponseType(typeof(ChecklistTemplateDTO), 200)]
    public async Task<IActionResult> SaveChecklistTemplate([FromBody] SaveChecklistTemplateRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(SaveChecklistTemplate)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _checklists.SaveTemplateAsync(scope.ToTheaterFilter(), theaterId, request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(SaveChecklistTemplate));
        }
    }

    /// <summary>Opens a showtime's checklist, creating the run from the active template the first time (every staff role).</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(ChecklistRunDTO), 200)]
    public async Task<IActionResult> OpenChecklist([FromBody] OpenChecklistRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(OpenChecklist)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _checklists.OpenAsync(theaterId, request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(OpenChecklist));
        }
    }

    /// <summary>Ticks or unticks one checklist item (every staff role).</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(ChecklistRunDTO), 200)]
    public async Task<IActionResult> SetChecklistItem([FromBody] SetChecklistItemRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(SetChecklistItem)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _checklists.SetItemAsync(scope.ToTheaterFilter(), User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(SetChecklistItem));
        }
    }

    /// <summary>Completes a checklist; every required item must be done (400 otherwise).</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(ChecklistRunDTO), 200)]
    public async Task<IActionResult> CompleteChecklist([FromBody] CompleteChecklistRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(CompleteChecklist)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            return Ok(await _checklists.CompleteAsync(scope.ToTheaterFilter(), User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(CompleteChecklist));
        }
    }
    #endregion

    #region Workforce

    /// <summary>Sets the caller's own manager-override PIN (approver roles only).</summary>
    [Authorize(Roles = RoleNames.Approvers)]
    [HttpPost]
    [ProducesResponseType(204)]
    public async Task<IActionResult> SetMyOverridePin([FromBody] SetOverridePinRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(SetMyOverridePin)} being awakened to process request...");
        try
        {
            await _overrides.SetPinAsync(User.GetUserId(), request.Pin);
            return NoContent();
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(SetMyOverridePin));
        }
    }

    /// <summary>Active approvers (Id, Name) who can authorise an override in the theater.</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(List<OverrideApproverDTO>), 200)]
    public async Task<IActionResult> GetOverrideApprovers([FromBody] OverrideApproversRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetOverrideApprovers)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _overrides.GetApproversAsync(theaterId));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetOverrideApprovers));
        }
    }

    // ── Roster (approvers manage it; every staff member reads their own shifts) ─

    /// <summary>Active staff of the theater, for the roster and task pickers (approvers only).</summary>
    [Authorize(Roles = RoleNames.Approvers)]
    [HttpPost]
    [ProducesResponseType(typeof(List<TheaterStaffDTO>), 200)]
    public async Task<IActionResult> GetTheaterStaff([FromBody] GetTheaterStaffRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetTheaterStaff)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _workforce.GetTheaterStaffAsync(theaterId));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetTheaterStaff));
        }
    }

    /// <summary>Shifts of a theater in a range of at most 31 days (approvers only).</summary>
    [Authorize(Roles = RoleNames.Approvers)]
    [HttpPost]
    [ProducesResponseType(typeof(List<StaffShiftDTO>), 200)]
    public async Task<IActionResult> GetRoster([FromBody] RosterRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetRoster)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _workforce.GetRosterAsync(theaterId, request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetRoster));
        }
    }

    /// <summary>Creates or edits a roster shift (approvers only: 403 for every other staff role).</summary>
    [Authorize(Roles = RoleNames.Approvers)]
    [HttpPost]
    [ProducesResponseType(typeof(StaffShiftDTO), 200)]
    public async Task<IActionResult> SaveShift([FromBody] SaveStaffShiftRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(SaveShift)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _workforce.SaveShiftAsync(scope.ToTheaterFilter(), theaterId, request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(SaveShift));
        }
    }

    /// <summary>Deletes a roster shift (approvers only).</summary>
    [Authorize(Roles = RoleNames.Approvers)]
    [HttpPost]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteShift([FromBody] DeleteStaffShiftRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(DeleteShift)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            await _workforce.DeleteShiftAsync(scope.ToTheaterFilter(), request);
            return NoContent();
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(DeleteShift));
        }
    }

    /// <summary>The caller's own shifts in a range of at most 31 days (every staff role).</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(List<StaffShiftDTO>), 200)]
    public async Task<IActionResult> GetMyShifts([FromBody] MyShiftsRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetMyShifts)} being awakened to process request...");
        try
        {
            return Ok(await _workforce.GetMyShiftsAsync(User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetMyShifts));
        }
    }

    // ── Time clock (every staff role; reporting only, never required to sell) ──

    /// <summary>Clocks the caller in. 400 when they are already clocked in.</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(TimeClockEntryDTO), 200)]
    public async Task<IActionResult> ClockIn([FromBody] ClockInRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(ClockIn)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _workforce.ClockInAsync(theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(ClockIn));
        }
    }

    /// <summary>Clocks the caller out. 400 when they are not clocked in.</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(TimeClockEntryDTO), 200)]
    public async Task<IActionResult> ClockOut([FromBody] ClockOutRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(ClockOut)} being awakened to process request...");
        try
        {
            return Ok(await _workforce.ClockOutAsync(User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(ClockOut));
        }
    }

    /// <summary>Whether the caller is clocked in (every staff role).</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(ClockStatusDTO), 200)]
    public async Task<IActionResult> GetMyClockStatus()
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetMyClockStatus)} being awakened to process request...");
        try
        {
            return Ok(await _workforce.GetMyClockStatusAsync(User.GetUserId()));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetMyClockStatus));
        }
    }

    /// <summary>Clock entries of a theater with worked minutes, range of at most 31 days (approvers only).</summary>
    [Authorize(Roles = RoleNames.Approvers)]
    [HttpPost]
    [ProducesResponseType(typeof(List<TimeClockEntryDTO>), 200)]
    public async Task<IActionResult> GetTimeSheet([FromBody] TimeSheetRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetTimeSheet)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _workforce.GetTimeSheetAsync(theaterId, request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetTimeSheet));
        }
    }

    // ── Tasks (approvers assign; every staff member works their own) ───────────

    /// <summary>Creates or edits a task assigned to a staff member, optionally linked to an incident or checklist run (approvers only).</summary>
    [Authorize(Roles = RoleNames.Approvers)]
    [HttpPost]
    [ProducesResponseType(typeof(StaffTaskDTO), 200)]
    public async Task<IActionResult> SaveTask([FromBody] SaveStaffTaskRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(SaveTask)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }
            var theaterId = scope.Resolve(request.TheaterId);
            return Ok(await _workforce.SaveTaskAsync(scope.ToTheaterFilter(), theaterId, User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(SaveTask));
        }
    }

    /// <summary>Task page, newest first. Filters: assignedTo, status, theaterId (must be in scope, else 403). Approvers only.</summary>
    [Authorize(Roles = RoleNames.Approvers)]
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<StaffTaskDTO>), 200)]
    public async Task<IActionResult> GetTasks([FromBody] PagingSearchDTO search)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetTasks)} being awakened to process request...");
        try
        {
            if (!User.TryGetStaffScope(out var scope))
            {
                return Forbid();
            }

            IReadOnlyCollection<Guid>? theaterIds = scope.ToTheaterFilter();
            if (search?.Filters != null
                && search.Filters.TryGetValue(_theaterIdFilter, out var requested)
                && Guid.TryParse(requested, out var requestedTheaterId))
            {
                theaterIds = new[] { scope.Resolve(requestedTheaterId) };
            }

            return Ok(await _workforce.GetTasksAsync(theaterIds, search ?? new PagingSearchDTO()));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetTasks));
        }
    }

    /// <summary>The caller's own tasks (every staff role).</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(List<StaffTaskDTO>), 200)]
    public async Task<IActionResult> GetMyTasks([FromBody] MyTasksRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(GetMyTasks)} being awakened to process request...");
        try
        {
            return Ok(await _workforce.GetMyTasksAsync(User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetMyTasks));
        }
    }

    /// <summary>Moves one of the caller's own tasks between Open, InProgress and Done (403 for someone else's task).</summary>
    [Authorize(Roles = RoleNames.StaffApp)]
    [HttpPost]
    [ProducesResponseType(typeof(StaffTaskDTO), 200)]
    public async Task<IActionResult> SetMyTaskStatus([FromBody] SetMyTaskStatusRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.{nameof(SetMyTaskStatus)} being awakened to process request...");
        try
        {
            return Ok(await _workforce.SetMyTaskStatusAsync(User.GetUserId(), request));
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(SetMyTaskStatus));
        }
    }
    #endregion
}

// ── Request classes ───────────────────────────────────────────────────────────

public record AddCommentRequest(string Content, Guid? ParentId);
public record ModerateCommentRequest(Guid CommentId, bool Approved);
public record DeleteCommentRequest(Guid CommentId);
public record RateMovieRequest(int Score, string? Review);

using Cinema.Business.Contracts;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.DTO.Movies;
using Cinema.Business.DTO.Requests;
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
public class CinemaController : ControllerBase
{
    private const string _adminRole = "Admin";

    private readonly IMovieManager   _movieManager;
    private readonly ITheaterManager _theaterManager;
    private readonly IAgeRestrictionManager _ageRestrictions;
    private readonly IDiscountTypeManager   _discountTypes;
    private readonly IMovieTypeManager      _movieTypes;
    private readonly ISeatTypeManager       _seatTypes;
    private readonly ITicketTypeManager     _ticketTypes;
    private readonly IUserTypeManager       _userTypes;
    private readonly IMemberShipManager     _memberShips;
    private readonly IHolidayManager        _holidays;
    private readonly INewsManager           _news;
    private readonly IDiscountManager       _discounts;
    private readonly IFoodAndDrinkManager   _foodAndDrinks;
    private readonly IRoomManager           _rooms;
    private readonly IShowTimeManager       _showTimes;
    private readonly IMovieTypeDetailManager     _movieTypeDetails;
    private readonly ISeatTypeTicketTypeManager  _seatTypeTicketTypes;
    private readonly IInvoiceAdminManager        _invoices;
    private readonly IWebHostEnvironment         _env;

    public CinemaController(
        IMovieManager movieManager,
        ITheaterManager theaterManager,
        IAgeRestrictionManager ageRestrictions,
        IDiscountTypeManager discountTypes,
        IMovieTypeManager movieTypes,
        ISeatTypeManager seatTypes,
        ITicketTypeManager ticketTypes,
        IUserTypeManager userTypes,
        IMemberShipManager memberShips,
        IHolidayManager holidays,
        INewsManager news,
        IDiscountManager discounts,
        IFoodAndDrinkManager foodAndDrinks,
        IRoomManager rooms,
        IShowTimeManager showTimes,
        IMovieTypeDetailManager movieTypeDetails,
        ISeatTypeTicketTypeManager seatTypeTicketTypes,
        IInvoiceAdminManager invoices,
        IWebHostEnvironment env)
    {
        _movieManager    = movieManager;
        _theaterManager  = theaterManager;
        _ageRestrictions = ageRestrictions;
        _discountTypes   = discountTypes;
        _movieTypes      = movieTypes;
        _seatTypes       = seatTypes;
        _ticketTypes     = ticketTypes;
        _userTypes       = userTypes;
        _memberShips     = memberShips;
        _holidays        = holidays;
        _news            = news;
        _discounts       = discounts;
        _foodAndDrinks   = foodAndDrinks;
        _rooms           = rooms;
        _showTimes       = showTimes;
        _movieTypeDetails    = movieTypeDetails;
        _seatTypeTicketTypes = seatTypeTicketTypes;
        _invoices            = invoices;
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
                return BadRequest("No file uploaded.");
            if (file.Length > _maxImageBytes)
                return BadRequest("File exceeds the 5 MB limit.");

            var ext = Path.GetExtension(file.FileName);
            if (!_allowedImageExtensions.Contains(ext) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only image files (jpg, png, webp, gif) are allowed.");

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
            LogProvider.Current.Fatal(e, $"{GetType().Name}.UploadImage->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
        }
    }

    // ── Movies ────────────────────────────────────────────────────────────────

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
            LogProvider.Current.Fatal(e, $"{GetType().Name}.GetMovies->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
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
            LogProvider.Current.Fatal(e, $"{GetType().Name}.GetNowShowingMovies->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
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
            LogProvider.Current.Fatal(e, $"{GetType().Name}.GetComingSoonMovies->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
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
            LogProvider.Current.Fatal(e, $"{GetType().Name}.GetMovie->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
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
            LogProvider.Current.Fatal(e, $"{GetType().Name}.CreateMovie->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
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
            LogProvider.Current.Fatal(e, $"{GetType().Name}.UpdateMovie->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
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
            LogProvider.Current.Fatal(e, $"{GetType().Name}.DeleteMovie->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
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
            LogProvider.Current.Fatal(e, $"{GetType().Name}.AddComment->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
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
            LogProvider.Current.Fatal(e, $"{GetType().Name}.RateMovie->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
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
            LogProvider.Current.Fatal(e, $"{GetType().Name}.GetTheaters->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
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
            LogProvider.Current.Fatal(e, $"{GetType().Name}.GetTheater->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
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
            LogProvider.Current.Fatal(e, $"{GetType().Name}.GetTheatersByMovie->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
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
            LogProvider.Current.Fatal(e, $"{GetType().Name}.CreateTheater->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
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
            LogProvider.Current.Fatal(e, $"{GetType().Name}.UpdateTheater->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
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
            LogProvider.Current.Fatal(e, $"{GetType().Name}.DeleteTheater->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
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
            LogProvider.Current.Fatal(e, $"{GetType().Name}.GetShowTimes->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
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
    public Task<IActionResult> GetAgeRestriction([FromQuery] Guid id)
    {
        return Run(nameof(GetAgeRestriction), () => _ageRestrictions.GetByIdAsync(id));
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
    public Task<IActionResult> UpdateAgeRestriction([FromBody] UpdateAgeRestrictionRequest request)
    {
        return Run(nameof(UpdateAgeRestriction), () => _ageRestrictions.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public Task<IActionResult> DeleteAgeRestriction([FromQuery] Guid id)
    {
        return RunNoContent(nameof(DeleteAgeRestriction), () => _ageRestrictions.DeleteAsync(id));
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
    public Task<IActionResult> GetDiscountType([FromQuery] Guid id)
    {
        return Run(nameof(GetDiscountType), () => _discountTypes.GetByIdAsync(id));
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
    public Task<IActionResult> UpdateDiscountType([FromBody] UpdateDiscountTypeRequest request)
    {
        return Run(nameof(UpdateDiscountType), () => _discountTypes.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public Task<IActionResult> DeleteDiscountType([FromQuery] Guid id)
    {
        return RunNoContent(nameof(DeleteDiscountType), () => _discountTypes.DeleteAsync(id));
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
    public Task<IActionResult> GetMovieType([FromQuery] Guid id)
    {
        return Run(nameof(GetMovieType), () => _movieTypes.GetByIdAsync(id));
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
    public Task<IActionResult> UpdateMovieType([FromBody] UpdateMovieTypeRequest request)
    {
        return Run(nameof(UpdateMovieType), () => _movieTypes.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public Task<IActionResult> DeleteMovieType([FromQuery] Guid id)
    {
        return RunNoContent(nameof(DeleteMovieType), () => _movieTypes.DeleteAsync(id));
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
    public Task<IActionResult> GetSeatType([FromQuery] Guid id)
    {
        return Run(nameof(GetSeatType), () => _seatTypes.GetByIdAsync(id));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(SeatTypeDTO), 200)]
    public Task<IActionResult> CreateSeatType([FromBody] CreateSeatTypeRequest request)
    {
        return Run(nameof(CreateSeatType), () => _seatTypes.CreateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPut]
    [ProducesResponseType(typeof(SeatTypeDTO), 200)]
    public Task<IActionResult> UpdateSeatType([FromBody] UpdateSeatTypeRequest request)
    {
        return Run(nameof(UpdateSeatType), () => _seatTypes.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public Task<IActionResult> DeleteSeatType([FromQuery] Guid id)
    {
        return RunNoContent(nameof(DeleteSeatType), () => _seatTypes.DeleteAsync(id));
    }
    #endregion

    #region TicketType
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<TicketTypeDTO>), 200)]
    public Task<IActionResult> GetTicketTypes([FromBody] PagingSearchDTO search)
    {
        return Run(nameof(GetTicketTypes), () => _ticketTypes.GetAsync(search));
    }

    [HttpGet]
    [ProducesResponseType(typeof(TicketTypeDTO), 200)]
    public Task<IActionResult> GetTicketType([FromQuery] Guid id)
    {
        return Run(nameof(GetTicketType), () => _ticketTypes.GetByIdAsync(id));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(TicketTypeDTO), 200)]
    public Task<IActionResult> CreateTicketType([FromBody] CreateTicketTypeRequest request)
    {
        return Run(nameof(CreateTicketType), () => _ticketTypes.CreateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPut]
    [ProducesResponseType(typeof(TicketTypeDTO), 200)]
    public Task<IActionResult> UpdateTicketType([FromBody] UpdateTicketTypeRequest request)
    {
        return Run(nameof(UpdateTicketType), () => _ticketTypes.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public Task<IActionResult> DeleteTicketType([FromQuery] Guid id)
    {
        return RunNoContent(nameof(DeleteTicketType), () => _ticketTypes.DeleteAsync(id));
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
    public Task<IActionResult> GetUserType([FromQuery] Guid id)
    {
        return Run(nameof(GetUserType), () => _userTypes.GetByIdAsync(id));
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
    public Task<IActionResult> UpdateUserType([FromBody] UpdateUserTypeRequest request)
    {
        return Run(nameof(UpdateUserType), () => _userTypes.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public Task<IActionResult> DeleteUserType([FromQuery] Guid id)
    {
        return RunNoContent(nameof(DeleteUserType), () => _userTypes.DeleteAsync(id));
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
    public Task<IActionResult> GetMemberShip([FromQuery] Guid id)
    {
        return Run(nameof(GetMemberShip), () => _memberShips.GetByIdAsync(id));
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
    public Task<IActionResult> UpdateMemberShip([FromBody] UpdateMemberShipRequest request)
    {
        return Run(nameof(UpdateMemberShip), () => _memberShips.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public Task<IActionResult> DeleteMemberShip([FromQuery] Guid id)
    {
        return RunNoContent(nameof(DeleteMemberShip), () => _memberShips.DeleteAsync(id));
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
    public Task<IActionResult> GetHoliday([FromQuery] Guid id)
    {
        return Run(nameof(GetHoliday), () => _holidays.GetByIdAsync(id));
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
    public Task<IActionResult> UpdateHoliday([FromBody] UpdateHolidayRequest request)
    {
        return Run(nameof(UpdateHoliday), () => _holidays.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public Task<IActionResult> DeleteHoliday([FromQuery] Guid id)
    {
        return RunNoContent(nameof(DeleteHoliday), () => _holidays.DeleteAsync(id));
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
    public Task<IActionResult> GetNews([FromQuery] Guid id)
    {
        return Run(nameof(GetNews), () => _news.GetByIdAsync(id));
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
    public Task<IActionResult> UpdateNews([FromBody] UpdateNewsRequest request)
    {
        return Run(nameof(UpdateNews), () => _news.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public Task<IActionResult> DeleteNews([FromQuery] Guid id)
    {
        return RunNoContent(nameof(DeleteNews), () => _news.DeleteAsync(id));
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
    public Task<IActionResult> GetDiscount([FromQuery] Guid id)
    {
        return Run(nameof(GetDiscount), () => _discounts.GetByIdAsync(id));
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
    public Task<IActionResult> UpdateDiscount([FromBody] UpdateDiscountRequest request)
    {
        return Run(nameof(UpdateDiscount), () => _discounts.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public Task<IActionResult> DeleteDiscount([FromQuery] Guid id)
    {
        return RunNoContent(nameof(DeleteDiscount), () => _discounts.DeleteAsync(id));
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
    public Task<IActionResult> GetFoodAndDrink([FromQuery] Guid id)
    {
        return Run(nameof(GetFoodAndDrink), () => _foodAndDrinks.GetByIdAsync(id));
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
    public Task<IActionResult> UpdateFoodAndDrink([FromBody] UpdateFoodAndDrinkRequest request)
    {
        return Run(nameof(UpdateFoodAndDrink), () => _foodAndDrinks.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public Task<IActionResult> DeleteFoodAndDrink([FromQuery] Guid id)
    {
        return RunNoContent(nameof(DeleteFoodAndDrink), () => _foodAndDrinks.DeleteAsync(id));
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
    public Task<IActionResult> GetRoom([FromQuery] Guid id)
    {
        return Run(nameof(GetRoom), () => _rooms.GetByIdAsync(id));
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
    public Task<IActionResult> UpdateRoom([FromBody] UpdateRoomRequest request)
    {
        return Run(nameof(UpdateRoom), () => _rooms.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public Task<IActionResult> DeleteRoom([FromQuery] Guid id)
    {
        return RunNoContent(nameof(DeleteRoom), () => _rooms.DeleteAsync(id));
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
    public Task<IActionResult> GetShowTime([FromQuery] Guid id)
    {
        return Run(nameof(GetShowTime), () => _showTimes.GetByIdAsync(id));
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
    public Task<IActionResult> UpdateShowTime([FromBody] UpdateShowTimeRequest request)
    {
        return Run(nameof(UpdateShowTime), () => _showTimes.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public Task<IActionResult> DeleteShowTime([FromQuery] Guid id)
    {
        return RunNoContent(nameof(DeleteShowTime), () => _showTimes.DeleteAsync(id));
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

    #region SeatTypeTicketType (price matrix)
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<SeatTypeTicketTypeDTO>), 200)]
    public Task<IActionResult> GetSeatTypeTicketTypes([FromBody] PagingSearchDTO search)
    {
        return Run(nameof(GetSeatTypeTicketTypes), () => _seatTypeTicketTypes.GetAsync(search));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(SeatTypeTicketTypeDTO), 200)]
    public Task<IActionResult> CreateSeatTypeTicketType([FromBody] CreateSeatTypeTicketTypeRequest request)
    {
        return Run(nameof(CreateSeatTypeTicketType), () => _seatTypeTicketTypes.CreateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpPut]
    [ProducesResponseType(typeof(SeatTypeTicketTypeDTO), 200)]
    public Task<IActionResult> UpdateSeatTypeTicketType([FromBody] UpdateSeatTypeTicketTypeRequest request)
    {
        return Run(nameof(UpdateSeatTypeTicketType), () => _seatTypeTicketTypes.UpdateAsync(request));
    }

    [Authorize(Roles = _adminRole)]
    [HttpDelete]
    [ProducesResponseType(204)]
    public Task<IActionResult> DeleteSeatTypeTicketType([FromQuery] Guid seatTypeId, [FromQuery] Guid ticketTypeId)
    {
        return RunNoContent(nameof(DeleteSeatTypeTicketType), () => _seatTypeTicketTypes.DeleteAsync(seatTypeId, ticketTypeId));
    }
    #endregion

    #region Invoice (admin: list / status / delete)
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
            LogProvider.Current.Fatal(e, $"{GetType().Name}.{action}->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
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
            LogProvider.Current.Fatal(e, $"{GetType().Name}.{action}->Exception: {e.GetType()}, {e.Message}");
            return StatusCode(StatusCodes.Status500InternalServerError, e.Message);
        }
    }
    #endregion
}

// ── Request classes ───────────────────────────────────────────────────────────

public record AddCommentRequest(string Content, Guid? ParentId);
public record RateMovieRequest(int Score, string? Review);

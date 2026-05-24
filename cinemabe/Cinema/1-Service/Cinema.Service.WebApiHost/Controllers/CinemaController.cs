using Cinema.Business.Contracts;
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

    public CinemaController(IMovieManager movieManager, ITheaterManager theaterManager)
    {
        _movieManager   = movieManager;
        _theaterManager = theaterManager;
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
}

// ── Request classes ───────────────────────────────────────────────────────────

public record AddCommentRequest(string Content, Guid? ParentId);
public record RateMovieRequest(int Score, string? Review);

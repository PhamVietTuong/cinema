using Cinema.Business.Contracts;
using Cinema.Business.DTO;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;
using Cinema.Foundation.Logging;
using Cinema.Service.WebApiHost.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cinema.Service.WebApiHost.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
[ApiExplorerSettings(GroupName = "identity")]
public class IdentityController : ApiControllerBase
{
    private const string _adminRole = "Admin";
    private const string _userRole  = "User";

    private readonly IAuthManager _authManager;

    public IdentityController(IAuthManager authManager) => _authManager = authManager;

    // ── Auth ──────────────────────────────────────────────────────────────────

    [HttpPost]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.Login being awakened to process request...");
        try
        {
            var result = await _authManager.LoginAsync(request);
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(Login));
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.Register being awakened to process request...");
        try
        {
            var result = await _authManager.RegisterAsync(request);
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(Register));
        }
    }

    // ── Profile ───────────────────────────────────────────────────────────────

    [Authorize]
    [HttpGet]
    [ProducesResponseType(typeof(UserDTO), 200)]
    public async Task<IActionResult> GetProfile()
    {
        LogProvider.Current.Information($"{GetType().Name}.GetProfile being awakened to process request...");
        try
        {
            var result = await _authManager.GetProfileAsync(User.GetUserId());
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetProfile));
        }
    }

    [Authorize]
    [HttpPut]
    [ProducesResponseType(204)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.UpdateProfile being awakened to process request...");
        try
        {
            await _authManager.UpdateProfileAsync(User.GetUserId(), request);
            return NoContent();
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(UpdateProfile));
        }
    }

    [Authorize]
    [HttpPost]
    [ProducesResponseType(204)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.ChangePassword being awakened to process request...");
        try
        {
            await _authManager.ChangePasswordAsync(User.GetUserId(), request);
            return NoContent();
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(ChangePassword));
        }
    }

    // ── Admin ───────────────────────────────────────────────────────────────────

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(DefaultSearchResults<UserDTO>), 200)]
    public async Task<IActionResult> GetUsers([FromBody] PagingSearchDTO search)
    {
        LogProvider.Current.Information($"{GetType().Name}.GetUsers being awakened to process request...");
        try
        {
            var result = await _authManager.GetUsersAsync(search);
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GetUsers));
        }
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(UserDTO), 200)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.CreateUser being awakened to process request...");
        try
        {
            var result = await _authManager.CreateUserAsync(request);
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(CreateUser));
        }
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(typeof(UserDTO), 200)]
    public async Task<IActionResult> UpdateUser([FromBody] UpdateUserRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.UpdateUser being awakened to process request...");
        try
        {
            var result = await _authManager.UpdateUserAsync(request);
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(UpdateUser));
        }
    }

    [Authorize(Roles = _adminRole)]
    [HttpPost]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteUser([FromQuery] Guid id)
    {
        LogProvider.Current.Information($"{GetType().Name}.DeleteUser being awakened to process request...");
        try
        {
            await _authManager.DeleteUserAsync(id);
            return NoContent();
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(DeleteUser));
        }
    }
}

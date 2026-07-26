using Cinema.Business.Contracts;
using Cinema.Business.DTO;
using Cinema.Business.DTO.Auth;
using Cinema.Business.DTO.Requests;
using Cinema.Data.Entities;
using Cinema.Foundation.Logging;
using Cinema.Service.WebApiHost.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
    [EnableRateLimiting(RateLimitPolicies.Auth)]
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
    [EnableRateLimiting(RateLimitPolicies.Auth)]
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

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.AuthEmail)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.ForgotPassword being awakened to process request...");
        try
        {
            await _authManager.RequestPasswordResetAsync(request);
            return NoContent();
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(ForgotPassword));
        }
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.ResetPassword being awakened to process request...");
        try
        {
            await _authManager.ResetPasswordAsync(request);
            return NoContent();
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(ResetPassword));
        }
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.ConfirmEmail being awakened to process request...");
        try
        {
            await _authManager.ConfirmEmailAsync(request);
            return NoContent();
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(ConfirmEmail));
        }
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.AuthEmail)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.ResendVerification being awakened to process request...");
        try
        {
            await _authManager.ResendVerificationAsync(request);
            return NoContent();
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(ResendVerification));
        }
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.GoogleLogin being awakened to process request...");
        try
        {
            var result = await _authManager.LoginWithGoogleAsync(request);
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(GoogleLogin));
        }
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    public async Task<IActionResult> FacebookLogin([FromBody] FacebookLoginRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.FacebookLogin being awakened to process request...");
        try
        {
            var result = await _authManager.LoginWithFacebookAsync(request);
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(FacebookLogin));
        }
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    public async Task<IActionResult> VerifyTwoFactor([FromBody] VerifyTwoFactorRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.VerifyTwoFactor being awakened to process request...");
        try
        {
            var result = await _authManager.VerifyTwoFactorAsync(request);
            return Ok(result);
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(VerifyTwoFactor));
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
    public async Task<IActionResult> UpdateNotificationPreferences([FromBody] UpdateNotificationPreferencesRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.UpdateNotificationPreferences being awakened to process request...");
        try
        {
            await _authManager.UpdateNotificationPreferencesAsync(User.GetUserId(), request);
            return NoContent();
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(UpdateNotificationPreferences));
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

    [Authorize]
    [HttpPost]
    [ProducesResponseType(204)]
    public async Task<IActionResult> SetTwoFactor([FromBody] SetTwoFactorRequest request)
    {
        LogProvider.Current.Information($"{GetType().Name}.SetTwoFactor being awakened to process request...");
        try
        {
            await _authManager.SetTwoFactorAsync(User.GetUserId(), request.Enabled);
            return NoContent();
        }
        catch (Exception e)
        {
            return HandleException(e, nameof(SetTwoFactor));
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

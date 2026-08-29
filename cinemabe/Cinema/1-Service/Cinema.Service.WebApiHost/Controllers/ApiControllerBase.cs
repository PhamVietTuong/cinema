using Cinema.Foundation.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cinema.Service.WebApiHost.Controllers;

/// <summary>
/// Base controller that centralises exception-to-status-code mapping so each action
/// can keep its try/catch while returning the correct HTTP status (not a blanket 500).
/// </summary>
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Checks existence before an id-based action (GetById/Update/Delete) calls into its manager.
    /// Returns a 404 <see cref="IActionResult"/> if the entity doesn't exist, or <c>null</c> if the
    /// caller should proceed.
    /// </summary>
    protected async Task<IActionResult?> EnsureExistsAsync(Func<Task<bool>> existsCheck, string action, string entityName, object id)
    {
        if (await existsCheck())
        {
            return null;
        }
        var message = $"{entityName} {id} not found.";
        LogProvider.Current.Warning($"{GetType().Name}.{action}->NotFound: {message}");
        return NotFound(new { error = message, statusCode = StatusCodes.Status404NotFound });
    }

    /// <summary>Maps a caught exception to the appropriate status code and logs it.</summary>
    protected IActionResult HandleException(Exception e, string action)
    {
        switch (e)
        {
            case KeyNotFoundException:
                LogProvider.Current.Warning(e, $"{GetType().Name}.{action}->NotFound: {e.Message}");
                return NotFound(new { error = e.Message, statusCode = StatusCodes.Status404NotFound });

            case UnauthorizedAccessException:
                LogProvider.Current.Warning(e, $"{GetType().Name}.{action}->Unauthorized: {e.Message}");
                return Unauthorized(new { error = e.Message, statusCode = StatusCodes.Status401Unauthorized });

            case InvalidOperationException:
                LogProvider.Current.Warning(e, $"{GetType().Name}.{action}->BadRequest: {e.Message}");
                return BadRequest(new { error = e.Message, statusCode = StatusCodes.Status400BadRequest });

            default:
                LogProvider.Current.Fatal(e, $"{GetType().Name}.{action}->Exception: {e.GetType()}, {e.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = e.Message, statusCode = StatusCodes.Status500InternalServerError });
        }
    }
}

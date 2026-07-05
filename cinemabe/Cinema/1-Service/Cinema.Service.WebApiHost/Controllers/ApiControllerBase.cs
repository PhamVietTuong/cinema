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

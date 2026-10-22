using Cinema.Service.WebApiHost.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Cinema.Service.WebApiHost.Tests;

internal sealed class Probe : ApiControllerBase
{
    public IActionResult Map(Exception e)
    {
        return HandleException(e, nameof(Map));
    }
}

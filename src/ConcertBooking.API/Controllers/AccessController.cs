using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConcertBooking.API.Controllers;

[ApiController]
[Route("api/access")]
public sealed class AccessController : ControllerBase
{
    [Authorize]
    [HttpGet("user")]
    public IActionResult UserAccess() => Ok(new { access = "user" });

    [Authorize(Roles = "Admin")]
    [HttpGet("admin")]
    public IActionResult AdminAccess() => Ok(new { access = "admin" });
}

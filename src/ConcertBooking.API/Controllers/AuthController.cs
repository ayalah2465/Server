using ConcertBooking.API.Models;
using ConcertBooking.Application.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace ConcertBooking.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(
            request.Email, request.DisplayName, request.Password, cancellationToken);
        if (result.Failure == AuthFailure.DuplicateEmail)
            return Conflict(new { message = "An account with this email already exists." });

        return StatusCode(StatusCodes.Status201Created,
            new AuthResponse(result.AccessToken!, result.ExpiresAtUtc!.Value));
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request.Email, request.Password, cancellationToken);
        if (result.Failure == AuthFailure.InvalidCredentials)
            return Unauthorized(new { message = "Invalid email or password." });

        return Ok(new AuthResponse(result.AccessToken!, result.ExpiresAtUtc!.Value));
    }
}

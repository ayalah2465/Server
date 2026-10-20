using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ConcertBooking.API.Models;
using ConcertBooking.Application.Bookings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConcertBooking.API.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class BookingsController(IBookingService service) : ControllerBase
{
    [HttpPost("concerts/{concertId:guid}/bookings")]
    public async Task<ActionResult<BookingDetails>> Create(
        [FromRoute] Guid concertId,
        [FromBody] SimulatedPaymentRequest payment,
        CancellationToken cancellationToken)
    {
        if (payment.Approved != true)
            return BadRequest(new { message = "Simulated payment was declined. No booking was created." });
        var userId = GetUserId();
        var result = await service.BookAsync(userId, concertId, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Failure switch
            {
                BookingFailure.ConcertNotFound => NotFound(),
                BookingFailure.UserNotFound => Unauthorized(),
                BookingFailure.ConcertStarted => BadRequest(new { message = "Concert has already started." }),
                _ => Conflict(new { message = "The concert is sold out, already booked, or changed concurrently." })
            };
        }
        var booking = await service.GetAsync(userId, result.BookingId!.Value, cancellationToken);
        return CreatedAtAction(nameof(Get), new { bookingId = result.BookingId.Value }, booking);
    }

    [HttpGet("bookings/{bookingId:guid}")]
    public async Task<ActionResult<BookingDetails>> Get(
        [FromRoute] Guid bookingId,
        CancellationToken cancellationToken)
    {
        var booking = await service.GetAsync(GetUserId(), bookingId, cancellationToken);
        return booking is null ? NotFound() : Ok(booking);
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : throw new InvalidOperationException("Authenticated token has no valid subject.");
    }
}

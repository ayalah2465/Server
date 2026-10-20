using ConcertBooking.Application.Common;
using ConcertBooking.Application.Concerts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConcertBooking.API.Controllers;

[ApiController]
[Route("api/concerts")]
public sealed class ConcertsController(IConcertService service) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<PagedResult<ConcertResponse>>> GetPage(
        [FromQuery] ConcertQuery query,
        CancellationToken cancellationToken)
    {
        if (query.FromUtc is not null && query.ToUtc is not null && query.FromUtc > query.ToUtc)
            return BadRequest(new { message = "FromUtc cannot be later than ToUtc." });
        return Ok(await service.GetPageAsync(query, cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ConcertResponse>> Get([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var concert = await service.GetAsync(id, cancellationToken);
        return concert is null ? NotFound() : Ok(concert);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<ConcertResponse>> Create(
        [FromBody] CreateConcertRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });
        return CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateConcertRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, request, cancellationToken);
        return result.Failure switch
        {
            OperationFailure.None => NoContent(),
            OperationFailure.NotFound => NotFound(),
            OperationFailure.Conflict => Conflict(new { message = result.Error }),
            _ => BadRequest(new { message = result.Error })
        };
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(id, cancellationToken);
        return result.Failure switch
        {
            OperationFailure.None => NoContent(),
            OperationFailure.NotFound => NotFound(),
            _ => Conflict(new { message = result.Error })
        };
    }
}

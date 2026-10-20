using ConcertBooking.Application.Common;
using ConcertBooking.Application.Concerts;
using ConcertBooking.Application.Instruments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConcertBooking.API.Controllers;

[ApiController]
[Route("api/instruments")]
public sealed class InstrumentsController(IInstrumentService service) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InstrumentResponse>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await service.GetAllAsync(cancellationToken));

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InstrumentResponse>> Get([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var instrument = await service.GetAsync(id, cancellationToken);
        return instrument is null ? NotFound() : Ok(instrument);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<InstrumentResponse>> Create(
        [FromBody] CreateInstrumentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value)
            : Conflict(new { message = result.Error });
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateInstrumentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, request, cancellationToken);
        return result.Failure switch
        {
            OperationFailure.None => NoContent(),
            OperationFailure.NotFound => NotFound(),
            _ => Conflict(new { message = result.Error })
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

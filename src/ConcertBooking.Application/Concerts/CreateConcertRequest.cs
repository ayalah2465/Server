using System.ComponentModel.DataAnnotations;

namespace ConcertBooking.Application.Concerts;

public class CreateConcertRequest
{
    [Required, StringLength(160, MinimumLength = 2)] public string Title { get; init; } = string.Empty;
    [Required, StringLength(2000)] public string Description { get; init; } = string.Empty;
    [Required, StringLength(200, MinimumLength = 2)] public string Venue { get; init; } = string.Empty;
    public DateTime StartsAtUtc { get; init; }
    [Range(1, 100000)] public int Capacity { get; init; }
    [Range(typeof(decimal), "0", "1000000")] public decimal TicketPrice { get; init; }
    [MinLength(1)] public IReadOnlyCollection<Guid> InstrumentIds { get; init; } = Array.Empty<Guid>();
}

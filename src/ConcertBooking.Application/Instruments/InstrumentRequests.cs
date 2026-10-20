using System.ComponentModel.DataAnnotations;

namespace ConcertBooking.Application.Instruments;

public class CreateInstrumentRequest
{
    [Required, StringLength(80, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;
}

public sealed class UpdateInstrumentRequest : CreateInstrumentRequest;

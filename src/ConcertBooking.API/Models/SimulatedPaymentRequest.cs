using System.ComponentModel.DataAnnotations;

namespace ConcertBooking.API.Models;

public sealed class SimulatedPaymentRequest
{
    [Required]
    public bool? Approved { get; init; }
}

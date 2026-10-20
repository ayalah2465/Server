namespace ConcertBooking.Application.Concerts;

public sealed record ConcertResponse(
    Guid Id,
    string Title,
    string Description,
    string Venue,
    DateTime StartsAtUtc,
    int Capacity,
    int BookedSeats,
    int RemainingSeats,
    decimal TicketPrice,
    IReadOnlyList<InstrumentResponse> Instruments);

public sealed record InstrumentResponse(Guid Id, string Name);

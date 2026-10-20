namespace ConcertBooking.Application.Bookings;

public sealed record BookingDetails(
    Guid Id,
    Guid ConcertId,
    string ConcertTitle,
    DateTime ConcertStartsAtUtc,
    DateTime CreatedAtUtc,
    decimal TicketPrice,
    string Status,
    string PaymentStatus);

namespace ConcertBooking.Application.Bookings;

public interface IBookingService
{
    Task<BookingResult> BookAsync(Guid userId, Guid concertId, CancellationToken cancellationToken = default);
    Task<BookingDetails?> GetAsync(Guid userId, Guid bookingId, CancellationToken cancellationToken = default);
}

namespace ConcertBooking.Application.Bookings;

public interface IBookingService
{
    Task<BookingResult> BookAsync(Guid userId, Guid concertId, CancellationToken cancellationToken = default);
}

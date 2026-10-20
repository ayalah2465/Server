using ConcertBooking.Domain.Entities;

namespace ConcertBooking.Application.Bookings;

public sealed class BookingService(IBookingRepository repository, TimeProvider timeProvider) : IBookingService
{
    public async Task<BookingResult> BookAsync(
        Guid userId,
        Guid concertId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await repository.BeginTransactionAsync(cancellationToken);
        var concert = await repository.GetConcertForBookingAsync(concertId, cancellationToken);
        if (concert is null)
            return BookingResult.Rejected(BookingFailure.ConcertNotFound);

        if (!await repository.UserExistsAsync(userId, cancellationToken))
            return BookingResult.Rejected(BookingFailure.UserNotFound);

        if (concert.StartsAtUtc <= timeProvider.GetUtcNow().UtcDateTime)
            return BookingResult.Rejected(BookingFailure.ConcertStarted);

        if (concert.BookedSeats >= concert.Capacity)
            return BookingResult.Rejected(BookingFailure.SoldOut);

        if (await repository.HasActiveBookingAsync(userId, concertId, cancellationToken))
            return BookingResult.Rejected(BookingFailure.AlreadyBooked);

        var booking = new Booking
        {
            UserId = userId,
            ConcertId = concertId,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            TicketPrice = concert.TicketPrice
        };
        concert.BookedSeats++;
        repository.Add(booking);

        try
        {
            await repository.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return BookingResult.Success(booking.Id, booking.TicketPrice);
        }
        catch (ConcurrencyConflictException)
        {
            return BookingResult.Rejected(BookingFailure.Conflict);
        }
    }

    public async Task<BookingDetails?> GetAsync(Guid userId, Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await repository.GetByIdAsync(bookingId, userId, cancellationToken);
        return booking is null ? null : new BookingDetails(
            booking.Id,
            booking.ConcertId,
            booking.Concert.Title,
            booking.Concert.StartsAtUtc,
            booking.CreatedAtUtc,
            booking.TicketPrice,
            booking.Status.ToString(),
            "SimulatedApproved");
    }
}

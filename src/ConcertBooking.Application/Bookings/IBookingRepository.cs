using ConcertBooking.Domain.Entities;

namespace ConcertBooking.Application.Bookings;

public interface IBookingRepository
{
    Task<Concert?> GetConcertForBookingAsync(Guid concertId, CancellationToken cancellationToken);
    Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> HasActiveBookingAsync(Guid userId, Guid concertId, CancellationToken cancellationToken);
    void Add(Booking booking);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}

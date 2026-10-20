using ConcertBooking.Application.Bookings;
using ConcertBooking.Domain.Entities;
using ConcertBooking.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace ConcertBooking.Infrastructure.Persistence;

public sealed class BookingRepository(AppDbContext dbContext) : IBookingRepository
{
    public Task<Concert?> GetConcertForBookingAsync(Guid concertId, CancellationToken cancellationToken) =>
        dbContext.Concerts.SingleOrDefaultAsync(concert => concert.Id == concertId, cancellationToken);

    public Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().AnyAsync(user => user.Id == userId, cancellationToken);

    public Task<bool> HasActiveBookingAsync(Guid userId, Guid concertId, CancellationToken cancellationToken) =>
        dbContext.Bookings.AsNoTracking().AnyAsync(
            booking => booking.UserId == userId && booking.ConcertId == concertId && booking.Status == BookingStatus.Confirmed,
            cancellationToken);

    public Task<Booking?> GetByIdAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.Bookings.AsNoTracking()
            .Include(booking => booking.Concert)
            .SingleOrDefaultAsync(booking => booking.Id == bookingId && booking.UserId == userId, cancellationToken);

    public void Add(Booking booking) => dbContext.Bookings.Add(booking);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException("The concert was changed by another booking.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        })
        {
            throw new ConcurrencyConflictException("An active booking already exists.", exception);
        }
    }

    public async Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        new ApplicationTransaction(await dbContext.Database.BeginTransactionAsync(cancellationToken));

    private sealed class ApplicationTransaction(IDbContextTransaction transaction) : IApplicationTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}

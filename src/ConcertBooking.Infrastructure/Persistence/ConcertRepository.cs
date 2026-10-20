using ConcertBooking.Application.Bookings;
using ConcertBooking.Application.Concerts;
using ConcertBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConcertBooking.Infrastructure.Persistence;

public sealed class ConcertRepository(AppDbContext dbContext) : IConcertRepository
{
    public async Task<(IReadOnlyList<Concert> Items, int TotalCount)> GetPageAsync(
        ConcertQuery query,
        CancellationToken cancellationToken)
    {
        var concerts = dbContext.Concerts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            concerts = concerts.Where(concert =>
                EF.Functions.ILike(concert.Title, pattern) || EF.Functions.ILike(concert.Venue, pattern));
        }
        if (!string.IsNullOrWhiteSpace(query.Instrument))
        {
            var instrument = query.Instrument.Trim();
            concerts = concerts.Where(concert => concert.Instruments.Any(item => EF.Functions.ILike(item.Name, instrument)));
        }
        if (query.FromUtc is not null) concerts = concerts.Where(concert => concert.StartsAtUtc >= query.FromUtc);
        if (query.ToUtc is not null) concerts = concerts.Where(concert => concert.StartsAtUtc <= query.ToUtc);

        concerts = (query.SortBy, query.SortDirection) switch
        {
            ("title", "asc") => concerts.OrderBy(concert => concert.Title).ThenBy(concert => concert.Id),
            ("title", "desc") => concerts.OrderByDescending(concert => concert.Title).ThenBy(concert => concert.Id),
            ("price", "asc") => concerts.OrderBy(concert => concert.TicketPrice).ThenBy(concert => concert.Id),
            ("price", "desc") => concerts.OrderByDescending(concert => concert.TicketPrice).ThenBy(concert => concert.Id),
            (_, "desc") => concerts.OrderByDescending(concert => concert.StartsAtUtc).ThenBy(concert => concert.Id),
            _ => concerts.OrderBy(concert => concert.StartsAtUtc).ThenBy(concert => concert.Id)
        };

        var totalCount = await concerts.CountAsync(cancellationToken);
        var items = await concerts
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(concert => concert.Instruments)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public Task<Concert?> GetByIdAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = dbContext.Concerts.Include(concert => concert.Instruments).AsSplitQuery();
        return (tracking ? query : query.AsNoTracking()).SingleOrDefaultAsync(concert => concert.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Instrument>> GetInstrumentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await dbContext.Instruments.Where(instrument => ids.Contains(instrument.Id)).ToListAsync(cancellationToken);

    public Task<bool> HasBookingsAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Bookings.AsNoTracking().AnyAsync(booking => booking.ConcertId == id, cancellationToken);

    public void Add(Concert concert) => dbContext.Concerts.Add(concert);
    public void Remove(Concert concert) => dbContext.Concerts.Remove(concert);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException("The concert changed concurrently.", exception);
        }
    }
}

using ConcertBooking.Application.Common;
using ConcertBooking.Application.Instruments;
using ConcertBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ConcertBooking.Infrastructure.Persistence;

public sealed class InstrumentRepository(AppDbContext dbContext) : IInstrumentRepository
{
    public async Task<IReadOnlyList<Instrument>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Instruments.AsNoTracking().OrderBy(instrument => instrument.Name).ToListAsync(cancellationToken);

    public Task<Instrument?> GetByIdAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = dbContext.Instruments.AsQueryable();
        return (tracking ? query : query.AsNoTracking()).SingleOrDefaultAsync(instrument => instrument.Id == id, cancellationToken);
    }

    public Task<bool> NameExistsAsync(string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        dbContext.Instruments.AsNoTracking().AnyAsync(
            instrument => instrument.Name.ToUpper() == normalizedName && (!exceptId.HasValue || instrument.Id != exceptId.Value),
            cancellationToken);

    public Task<bool> IsUsedAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Concerts.AsNoTracking().AnyAsync(concert => concert.Instruments.Any(instrument => instrument.Id == id), cancellationToken);

    public void Add(Instrument instrument) => dbContext.Instruments.Add(instrument);
    public void Remove(Instrument instrument) => dbContext.Instruments.Remove(instrument);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        })
        {
            throw new ResourceConflictException("Instrument name already exists.", exception);
        }
    }
}

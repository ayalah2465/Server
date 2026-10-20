using ConcertBooking.Domain.Entities;

namespace ConcertBooking.Application.Instruments;

public interface IInstrumentRepository
{
    Task<IReadOnlyList<Instrument>> GetAllAsync(CancellationToken cancellationToken);
    Task<Instrument?> GetByIdAsync(Guid id, bool tracking, CancellationToken cancellationToken);
    Task<bool> NameExistsAsync(string normalizedName, Guid? exceptId, CancellationToken cancellationToken);
    Task<bool> IsUsedAsync(Guid id, CancellationToken cancellationToken);
    void Add(Instrument instrument);
    void Remove(Instrument instrument);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

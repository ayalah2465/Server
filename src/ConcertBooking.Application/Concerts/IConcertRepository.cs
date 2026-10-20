using ConcertBooking.Domain.Entities;

namespace ConcertBooking.Application.Concerts;

public interface IConcertRepository
{
    Task<(IReadOnlyList<Concert> Items, int TotalCount)> GetPageAsync(ConcertQuery query, CancellationToken cancellationToken);
    Task<Concert?> GetByIdAsync(Guid id, bool tracking, CancellationToken cancellationToken);
    Task<IReadOnlyList<Instrument>> GetInstrumentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    Task<bool> HasBookingsAsync(Guid id, CancellationToken cancellationToken);
    void Add(Concert concert);
    void Remove(Concert concert);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

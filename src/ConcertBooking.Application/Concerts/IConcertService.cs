using ConcertBooking.Application.Common;

namespace ConcertBooking.Application.Concerts;

public interface IConcertService
{
    Task<PagedResult<ConcertResponse>> GetPageAsync(ConcertQuery query, CancellationToken cancellationToken);
    Task<ConcertResponse?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<OperationResult<ConcertResponse>> CreateAsync(CreateConcertRequest request, CancellationToken cancellationToken);
    Task<OperationResult<bool>> UpdateAsync(Guid id, UpdateConcertRequest request, CancellationToken cancellationToken);
    Task<OperationResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

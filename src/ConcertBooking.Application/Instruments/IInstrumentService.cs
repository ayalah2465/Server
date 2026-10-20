using ConcertBooking.Application.Common;
using ConcertBooking.Application.Concerts;

namespace ConcertBooking.Application.Instruments;

public interface IInstrumentService
{
    Task<IReadOnlyList<InstrumentResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<InstrumentResponse?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<OperationResult<InstrumentResponse>> CreateAsync(CreateInstrumentRequest request, CancellationToken cancellationToken);
    Task<OperationResult<bool>> UpdateAsync(Guid id, UpdateInstrumentRequest request, CancellationToken cancellationToken);
    Task<OperationResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

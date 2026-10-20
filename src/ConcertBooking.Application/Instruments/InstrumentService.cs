using AutoMapper;
using ConcertBooking.Application.Common;
using ConcertBooking.Application.Concerts;
using ConcertBooking.Domain.Entities;

namespace ConcertBooking.Application.Instruments;

public sealed class InstrumentService(IInstrumentRepository repository, IMapper mapper) : IInstrumentService
{
    public async Task<IReadOnlyList<InstrumentResponse>> GetAllAsync(CancellationToken cancellationToken) =>
        mapper.Map<List<InstrumentResponse>>(await repository.GetAllAsync(cancellationToken));

    public async Task<InstrumentResponse?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        mapper.Map<InstrumentResponse?>(await repository.GetByIdAsync(id, false, cancellationToken));

    public async Task<OperationResult<InstrumentResponse>> CreateAsync(CreateInstrumentRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await repository.NameExistsAsync(name.ToUpperInvariant(), null, cancellationToken))
            return OperationResult<InstrumentResponse>.Reject(OperationFailure.Conflict, "Instrument name already exists.");
        var instrument = new Instrument { Name = name };
        repository.Add(instrument);
        try { await repository.SaveChangesAsync(cancellationToken); }
        catch (ResourceConflictException) { return OperationResult<InstrumentResponse>.Reject(OperationFailure.Conflict, "Instrument name already exists."); }
        return OperationResult<InstrumentResponse>.Success(mapper.Map<InstrumentResponse>(instrument));
    }

    public async Task<OperationResult<bool>> UpdateAsync(Guid id, UpdateInstrumentRequest request, CancellationToken cancellationToken)
    {
        var instrument = await repository.GetByIdAsync(id, true, cancellationToken);
        if (instrument is null) return OperationResult<bool>.Reject(OperationFailure.NotFound, "Instrument not found.");
        var name = request.Name.Trim();
        if (await repository.NameExistsAsync(name.ToUpperInvariant(), id, cancellationToken))
            return OperationResult<bool>.Reject(OperationFailure.Conflict, "Instrument name already exists.");
        instrument.Name = name;
        try { await repository.SaveChangesAsync(cancellationToken); }
        catch (ResourceConflictException) { return OperationResult<bool>.Reject(OperationFailure.Conflict, "Instrument name already exists."); }
        return OperationResult<bool>.Success(true);
    }

    public async Task<OperationResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var instrument = await repository.GetByIdAsync(id, true, cancellationToken);
        if (instrument is null) return OperationResult<bool>.Reject(OperationFailure.NotFound, "Instrument not found.");
        if (await repository.IsUsedAsync(id, cancellationToken))
            return OperationResult<bool>.Reject(OperationFailure.Conflict, "An instrument assigned to concerts cannot be deleted.");
        repository.Remove(instrument);
        await repository.SaveChangesAsync(cancellationToken);
        return OperationResult<bool>.Success(true);
    }
}

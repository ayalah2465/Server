using AutoMapper;
using ConcertBooking.Application.Bookings;
using ConcertBooking.Application.Common;
using ConcertBooking.Domain.Entities;

namespace ConcertBooking.Application.Concerts;

public sealed class ConcertService(IConcertRepository repository, IMapper mapper, TimeProvider timeProvider) : IConcertService
{
    public async Task<PagedResult<ConcertResponse>> GetPageAsync(ConcertQuery query, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await repository.GetPageAsync(query, cancellationToken);
        return new PagedResult<ConcertResponse>(mapper.Map<List<ConcertResponse>>(items), query.Page, query.PageSize, totalCount);
    }

    public async Task<ConcertResponse?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        mapper.Map<ConcertResponse?>(await repository.GetByIdAsync(id, false, cancellationToken));

    public async Task<OperationResult<ConcertResponse>> CreateAsync(CreateConcertRequest request, CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(request, 0, cancellationToken);
        if (validation.Error is not null) return OperationResult<ConcertResponse>.Reject(OperationFailure.Invalid, validation.Error);
        var concert = BuildConcert(request, validation.Instruments!);
        repository.Add(concert);
        await repository.SaveChangesAsync(cancellationToken);
        return OperationResult<ConcertResponse>.Success(mapper.Map<ConcertResponse>(concert));
    }

    public async Task<OperationResult<bool>> UpdateAsync(Guid id, UpdateConcertRequest request, CancellationToken cancellationToken)
    {
        var concert = await repository.GetByIdAsync(id, true, cancellationToken);
        if (concert is null) return OperationResult<bool>.Reject(OperationFailure.NotFound, "Concert not found.");
        if (request.Capacity < concert.BookedSeats)
            return OperationResult<bool>.Reject(OperationFailure.Invalid, "Capacity cannot be lower than booked seats.");
        var validation = await ValidateAsync(request, concert.BookedSeats, cancellationToken);
        if (validation.Error is not null) return OperationResult<bool>.Reject(OperationFailure.Invalid, validation.Error);
        concert.Title = request.Title.Trim();
        concert.Description = request.Description.Trim();
        concert.Venue = request.Venue.Trim();
        concert.StartsAtUtc = request.StartsAtUtc;
        concert.Capacity = request.Capacity;
        concert.TicketPrice = request.TicketPrice;
        concert.Instruments.Clear();
        foreach (var instrument in validation.Instruments!) concert.Instruments.Add(instrument);
        try { await repository.SaveChangesAsync(cancellationToken); }
        catch (ConcurrencyConflictException) { return OperationResult<bool>.Reject(OperationFailure.Conflict, "Concert changed concurrently."); }
        return OperationResult<bool>.Success(true);
    }

    public async Task<OperationResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var concert = await repository.GetByIdAsync(id, true, cancellationToken);
        if (concert is null) return OperationResult<bool>.Reject(OperationFailure.NotFound, "Concert not found.");
        if (await repository.HasBookingsAsync(id, cancellationToken))
            return OperationResult<bool>.Reject(OperationFailure.Conflict, "A concert with booking history cannot be deleted.");
        repository.Remove(concert);
        await repository.SaveChangesAsync(cancellationToken);
        return OperationResult<bool>.Success(true);
    }

    private async Task<(IReadOnlyList<Instrument>? Instruments, string? Error)> ValidateAsync(
        CreateConcertRequest request, int bookedSeats, CancellationToken cancellationToken)
    {
        if (request.StartsAtUtc.Kind != DateTimeKind.Utc)
            return (null, "StartsAtUtc must be UTC.");
        if (request.StartsAtUtc <= timeProvider.GetUtcNow().UtcDateTime)
            return (null, "Concert must start in the future.");
        if (request.Capacity < bookedSeats)
            return (null, "Capacity cannot be lower than booked seats.");
        var distinctIds = request.InstrumentIds.Distinct().ToArray();
        var instruments = await repository.GetInstrumentsAsync(distinctIds, cancellationToken);
        if (distinctIds.Length == 0 || instruments.Count != distinctIds.Length)
            return (null, "Every instrument must exist and at least one is required.");
        return (instruments, null);
    }

    private static Concert BuildConcert(CreateConcertRequest request, IReadOnlyList<Instrument> instruments) => new()
    {
        Title = request.Title.Trim(), Description = request.Description.Trim(), Venue = request.Venue.Trim(),
        StartsAtUtc = request.StartsAtUtc, Capacity = request.Capacity, TicketPrice = request.TicketPrice,
        Instruments = instruments.ToList()
    };
}

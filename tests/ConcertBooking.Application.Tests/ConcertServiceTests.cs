using AutoMapper;
using ConcertBooking.Application.Common;
using ConcertBooking.Application.Concerts;
using ConcertBooking.Domain.Entities;
using Moq;

namespace ConcertBooking.Application.Tests;

public sealed class ConcertServiceTests
{
    private readonly Mock<IConcertRepository> repository = new(MockBehavior.Strict);
    private readonly Mock<IMapper> mapper = new(MockBehavior.Strict);
    private readonly DateTimeOffset now = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_rejects_non_utc_start_before_database_queries()
    {
        var request = ValidCreateRequest().WithStartsAt(DateTime.SpecifyKind(now.AddDays(1).DateTime, DateTimeKind.Unspecified));

        var result = await CreateService().CreateAsync(request, CancellationToken.None);

        Assert.Equal(OperationFailure.Invalid, result.Failure);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Update_rejects_capacity_below_existing_bookings()
    {
        var id = Guid.NewGuid();
        repository.Setup(item => item.GetByIdAsync(id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Concert { Id = id, BookedSeats = 3 });
        var request = ValidCreateRequest(capacity: 2);

        var result = await CreateService().UpdateAsync(id, new UpdateConcertRequest
        {
            Title = request.Title, Description = request.Description, Venue = request.Venue,
            StartsAtUtc = request.StartsAtUtc, Capacity = request.Capacity,
            TicketPrice = request.TicketPrice, InstrumentIds = request.InstrumentIds
        }, CancellationToken.None);

        Assert.Equal(OperationFailure.Invalid, result.Failure);
        repository.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_rejects_concert_with_booking_history()
    {
        var id = Guid.NewGuid();
        var concert = new Concert { Id = id };
        repository.Setup(item => item.GetByIdAsync(id, true, It.IsAny<CancellationToken>())).ReturnsAsync(concert);
        repository.Setup(item => item.HasBookingsAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateService().DeleteAsync(id, CancellationToken.None);

        Assert.Equal(OperationFailure.Conflict, result.Failure);
        repository.Verify(item => item.Remove(It.IsAny<Concert>()), Times.Never);
    }

    [Fact]
    public async Task GetPage_passes_query_and_cancellation_token_to_repository()
    {
        using var cancellation = new CancellationTokenSource();
        var query = new ConcertQuery { Page = 2, PageSize = 5 };
        repository.Setup(item => item.GetPageAsync(query, cancellation.Token))
            .ReturnsAsync((Array.Empty<Concert>(), 8));
        mapper.Setup(item => item.Map<List<ConcertResponse>>(It.IsAny<object>())).Returns([]);

        var result = await CreateService().GetPageAsync(query, cancellation.Token);

        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.TotalPages);
        repository.VerifyAll();
    }

    private ConcertService CreateService() => new(repository.Object, mapper.Object, new FixedTimeProvider(now));

    private CreateConcertRequest ValidCreateRequest(int capacity = 10) => new()
    {
        Title = "Test Concert", Description = "Description", Venue = "Venue",
        StartsAtUtc = now.AddDays(1).UtcDateTime, Capacity = capacity, TicketPrice = 25m,
        InstrumentIds = new[] { Guid.NewGuid() }
    };

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}

internal static class ConcertRequestTestExtensions
{
    public static CreateConcertRequest WithStartsAt(this CreateConcertRequest request, DateTime startsAtUtc) => new()
    {
        Title = request.Title, Description = request.Description, Venue = request.Venue,
        StartsAtUtc = startsAtUtc, Capacity = request.Capacity, TicketPrice = request.TicketPrice,
        InstrumentIds = request.InstrumentIds
    };
}

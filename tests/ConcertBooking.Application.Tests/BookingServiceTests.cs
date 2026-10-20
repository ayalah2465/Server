using ConcertBooking.Application.Bookings;
using ConcertBooking.Domain.Entities;
using Moq;

namespace ConcertBooking.Application.Tests;

public sealed class BookingServiceTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly Mock<IBookingRepository> repository = new(MockBehavior.Strict);
    private readonly Mock<IApplicationTransaction> transaction = new(MockBehavior.Strict);
    private readonly Guid userId = Guid.NewGuid();
    private readonly Guid concertId = Guid.NewGuid();

    public BookingServiceTests()
    {
        transaction.Setup(item => item.DisposeAsync()).Returns(ValueTask.CompletedTask);
        repository.Setup(item => item.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);
    }

    [Fact]
    public async Task BookAsync_creates_booking_at_the_current_price_and_increments_capacity()
    {
        var concert = FutureConcert(capacity: 2, bookedSeats: 1);
        Booking? added = null;
        repository.Setup(item => item.GetConcertForBookingAsync(concertId, It.IsAny<CancellationToken>())).ReturnsAsync(concert);
        repository.Setup(item => item.UserExistsAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(item => item.HasActiveBookingAsync(userId, concertId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repository.Setup(item => item.Add(It.IsAny<Booking>())).Callback<Booking>(booking => added = booking);
        repository.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        transaction.Setup(item => item.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateService().BookAsync(userId, concertId);

        Assert.True(result.IsSuccess);
        Assert.Equal(result.BookingId, added!.Id);
        Assert.Equal(42.50m, added.TicketPrice);
        Assert.Equal(Now.UtcDateTime, added.CreatedAtUtc);
        Assert.Equal(2, concert.BookedSeats);
        repository.VerifyAll();
        transaction.VerifyAll();
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    public async Task BookAsync_rejects_when_capacity_is_unavailable(int capacity, int bookedSeats)
    {
        var concert = FutureConcert(capacity, bookedSeats);
        repository.Setup(item => item.GetConcertForBookingAsync(concertId, It.IsAny<CancellationToken>())).ReturnsAsync(concert);
        repository.Setup(item => item.UserExistsAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateService().BookAsync(userId, concertId);

        Assert.Equal(BookingFailure.SoldOut, result.Failure);
        repository.Verify(item => item.Add(It.IsAny<Booking>()), Times.Never);
    }

    [Fact]
    public async Task BookAsync_rejects_duplicate_active_booking()
    {
        var concert = FutureConcert(2, 0);
        repository.Setup(item => item.GetConcertForBookingAsync(concertId, It.IsAny<CancellationToken>())).ReturnsAsync(concert);
        repository.Setup(item => item.UserExistsAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(item => item.HasActiveBookingAsync(userId, concertId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateService().BookAsync(userId, concertId);

        Assert.Equal(BookingFailure.AlreadyBooked, result.Failure);
        Assert.Equal(0, concert.BookedSeats);
    }

    [Fact]
    public async Task BookAsync_translates_concurrent_save_to_conflict()
    {
        var concert = FutureConcert(1, 0);
        repository.Setup(item => item.GetConcertForBookingAsync(concertId, It.IsAny<CancellationToken>())).ReturnsAsync(concert);
        repository.Setup(item => item.UserExistsAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(item => item.HasActiveBookingAsync(userId, concertId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repository.Setup(item => item.Add(It.IsAny<Booking>()));
        repository.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("conflict"));

        var result = await CreateService().BookAsync(userId, concertId);

        Assert.Equal(BookingFailure.Conflict, result.Failure);
        transaction.Verify(item => item.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BookAsync_rejects_missing_concert_before_other_queries()
    {
        repository.Setup(item => item.GetConcertForBookingAsync(concertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Concert?)null);

        var result = await CreateService().BookAsync(userId, concertId);

        Assert.Equal(BookingFailure.ConcertNotFound, result.Failure);
        repository.Verify(item => item.UserExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BookAsync_rejects_missing_user()
    {
        repository.Setup(item => item.GetConcertForBookingAsync(concertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FutureConcert(1, 0));
        repository.Setup(item => item.UserExistsAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateService().BookAsync(userId, concertId);

        Assert.Equal(BookingFailure.UserNotFound, result.Failure);
    }

    [Fact]
    public async Task BookAsync_rejects_concert_that_has_started()
    {
        var concert = FutureConcert(1, 0);
        concert.StartsAtUtc = Now.UtcDateTime;
        repository.Setup(item => item.GetConcertForBookingAsync(concertId, It.IsAny<CancellationToken>())).ReturnsAsync(concert);
        repository.Setup(item => item.UserExistsAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateService().BookAsync(userId, concertId);

        Assert.Equal(BookingFailure.ConcertStarted, result.Failure);
    }

    private BookingService CreateService() => new(repository.Object, new FixedTimeProvider(Now));

    private Concert FutureConcert(int capacity, int bookedSeats) => new()
    {
        Id = concertId,
        Title = "Test",
        Venue = "Test venue",
        StartsAtUtc = Now.AddDays(1).UtcDateTime,
        Capacity = capacity,
        BookedSeats = bookedSeats,
        TicketPrice = 42.50m
    };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

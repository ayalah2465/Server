using ConcertBooking.Domain.Entities;
using ConcertBooking.Domain.Enums;
using ConcertBooking.Application.Bookings;
using ConcertBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ConcertBooking.Infrastructure.Tests;

// Each test owns a disposable database. A missing PostgreSQL connection is a failure, not a skip.
public sealed class DatabaseTests : IAsyncLifetime
{
    private readonly string databaseName = $"concert_test_{Guid.NewGuid():N}";
    private string adminConnectionString = string.Empty;
    private string testConnectionString = string.Empty;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("CONCERT_TEST_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Set CONCERT_TEST_CONNECTION_STRING to a local PostgreSQL connection with CREATEDB permission. Tests create and drop only their own concert_test_* databases.");
        var builder = new NpgsqlConnectionStringBuilder(configured) { Database = "postgres", Pooling = false };
        adminConnectionString = builder.ConnectionString;
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();
        builder.Database = databaseName;
        testConnectionString = builder.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(adminConnectionString)) return;
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    private AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(testConnectionString).Options);

    [Fact]
    public async Task Migrations_create_a_fresh_database_without_pending_model_changes()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        Assert.Equal(2, (await db.Database.GetAppliedMigrationsAsync()).Count());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(0, await db.Users.CountAsync());
        Assert.Equal(0, await db.Bookings.CountAsync());
    }

    [Fact]
    public async Task Seed_can_run_twice_without_duplicates_or_overwriting_existing_changes()
    {
        await using (var db = CreateContext())
        {
            await new DatabaseInitializer(db).InitializeAsync();
            var concert = await db.Concerts.SingleAsync();
            concert.Title = "Edited demo title";
            await db.SaveChangesAsync();
        }
        await using (var db = CreateContext())
        {
            await new DatabaseInitializer(db).InitializeAsync();
        }
        await using var verification = CreateContext();
        Assert.Equal(3, await verification.Instruments.CountAsync());
        Assert.Equal(2, await verification.Users.CountAsync());
        Assert.Equal(2, await verification.Users.Select(user => user.NormalizedEmail).Distinct().CountAsync());
        var saved = await verification.Concerts.AsNoTracking().Include(concert => concert.Instruments).SingleAsync();
        Assert.Equal("Edited demo title", saved.Title);
        Assert.Equal(3, saved.Instruments.Count);
        Assert.Equal(30, saved.Capacity);
        Assert.Equal(0, saved.BookedSeats);
        Assert.Equal(75m, saved.TicketPrice);
        Assert.Equal(DateTimeKind.Utc, saved.StartsAtUtc.Kind);
    }

    [Fact]
    public async Task Relationships_round_trip_one_to_many_and_many_to_many()
    {
        Guid concertId;
        Guid userId;
        await using (var db = CreateContext())
        {
            await new DatabaseInitializer(db).InitializeAsync();
            var concert = await db.Concerts.Include(item => item.Instruments).SingleAsync();
            var user = NewUser();
            db.Bookings.Add(new Booking { User = user, Concert = concert, TicketPrice = concert.TicketPrice });
            concert.BookedSeats++;
            await db.SaveChangesAsync();
            concertId = concert.Id;
            userId = user.Id;
        }
        await using var verification = CreateContext();
        var saved = await verification.Concerts.AsNoTracking()
            .Include(concert => concert.Instruments)
            .Include(concert => concert.Bookings).ThenInclude(booking => booking.User)
            .AsSplitQuery().SingleAsync(concert => concert.Id == concertId);
        Assert.Equal(3, saved.Instruments.Count);
        Assert.Equal(userId, Assert.Single(saved.Bookings).User.Id);
        Assert.Equal(DateTimeKind.Utc, saved.Bookings.Single().CreatedAtUtc.Kind);
        Assert.Single(await verification.Instruments.AsNoTracking()
            .Where(instrument => instrument.Concerts.Any(concert => concert.Id == concertId))
            .Where(instrument => instrument.Name == "Piano").ToListAsync());
    }

    [Theory]
    [InlineData(0, 0, 10, "CK_Concerts_Capacity")]
    [InlineData(2, 3, 10, "CK_Concerts_BookedSeats")]
    [InlineData(2, -1, 10, "CK_Concerts_BookedSeats")]
    [InlineData(2, 0, -1, "CK_Concerts_TicketPrice")]
    public async Task Database_rejects_invalid_concert_values(int capacity, int bookedSeats, int price, string constraint)
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        db.Concerts.Add(new Concert
        {
            Title = "Invalid data test", Venue = "Test venue", StartsAtUtc = DateTime.UtcNow.AddDays(1),
            Capacity = capacity, BookedSeats = bookedSeats, TicketPrice = price
        });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, postgres.SqlState);
        Assert.Equal(constraint, postgres.ConstraintName);
    }

    [Fact]
    public async Task Database_rejects_duplicate_active_booking_but_preserves_cancelled_history()
    {
        await using var db = CreateContext();
        await new DatabaseInitializer(db).InitializeAsync();
        var concert = await db.Concerts.SingleAsync();
        var user = NewUser();
        db.Bookings.Add(new Booking { User = user, Concert = concert, Status = BookingStatus.Cancelled });
        db.Bookings.Add(new Booking { User = user, Concert = concert });
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.Bookings.CountAsync());

        db.Bookings.Add(new Booking { UserId = user.Id, ConcertId = concert.Id });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task Database_rejects_booking_with_missing_user()
    {
        await using var db = CreateContext();
        await new DatabaseInitializer(db).InitializeAsync();
        db.Bookings.Add(new Booking { UserId = Guid.NewGuid(), ConcertId = await db.Concerts.Select(c => c.Id).SingleAsync() });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task Database_rejects_duplicate_normalized_email()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        db.Users.AddRange(NewUser(), NewUser());
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task Two_contexts_updating_the_same_concert_detect_the_second_save()
    {
        await using (var setup = CreateContext())
            await new DatabaseInitializer(setup).InitializeAsync();

        await using var first = CreateContext();
        await using var second = CreateContext();
        var firstConcert = await first.Concerts.SingleAsync();
        var secondConcert = await second.Concerts.SingleAsync();
        var originalVersion = firstConcert.Version;
        firstConcert.BookedSeats++;
        secondConcert.BookedSeats++;

        await first.SaveChangesAsync();
        var exception = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        Assert.Single(exception.Entries);
        Assert.NotEqual(originalVersion, firstConcert.Version);
        await using var verification = CreateContext();
        Assert.Equal(1, await verification.Concerts.Select(concert => concert.BookedSeats).SingleAsync());
    }

    [Fact]
    public async Task Two_users_competing_for_last_seat_produce_one_booking_and_one_conflict()
    {
        Guid concertId;
        Guid firstUserId;
        Guid secondUserId;
        await using (var setup = CreateContext())
        {
            await new DatabaseInitializer(setup).InitializeAsync();
            var concert = await setup.Concerts.SingleAsync();
            concert.Capacity = 1;
            var firstUser = NewUser("first@example.invalid");
            var secondUser = NewUser("second@example.invalid");
            setup.Users.AddRange(firstUser, secondUser);
            await setup.SaveChangesAsync();
            concertId = concert.Id;
            firstUserId = firstUser.Id;
            secondUserId = secondUser.Id;
        }

        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var firstRepository = new CoordinatedBookingRepository(new BookingRepository(firstContext));
        var secondRepository = new CoordinatedBookingRepository(new BookingRepository(secondContext));
        var firstService = new BookingService(firstRepository, TimeProvider.System);
        var secondService = new BookingService(secondRepository, TimeProvider.System);

        var firstTask = firstService.BookAsync(firstUserId, concertId);
        var secondTask = secondService.BookAsync(secondUserId, concertId);
        await Task.WhenAll(firstRepository.Ready, secondRepository.Ready);
        firstRepository.Release();
        var firstResult = await firstTask;
        secondRepository.Release();
        var secondResult = await secondTask;

        Assert.True(firstResult.IsSuccess);
        Assert.Equal(BookingFailure.Conflict, secondResult.Failure);
        await using var verification = CreateContext();
        Assert.Equal(1, await verification.Concerts.Select(concert => concert.BookedSeats).SingleAsync());
        Assert.Equal(1, await verification.Bookings.CountAsync());
    }

    private static User NewUser(string email = "test@example.invalid") => new()
    {
        Email = email, NormalizedEmail = email.ToUpperInvariant(),
        DisplayName = "Test User", PasswordHash = "TEST-ONLY-NOT-A-LOGIN-HASH"
    };

    private sealed class CoordinatedBookingRepository(IBookingRepository inner) : IBookingRepository
    {
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Ready => ready.Task;
        public void Release() => release.TrySetResult();
        public Task<Concert?> GetConcertForBookingAsync(Guid concertId, CancellationToken cancellationToken) => inner.GetConcertForBookingAsync(concertId, cancellationToken);
        public Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken) => inner.UserExistsAsync(userId, cancellationToken);
        public async Task<bool> HasActiveBookingAsync(Guid userId, Guid concertId, CancellationToken cancellationToken)
        {
            var result = await inner.HasActiveBookingAsync(userId, concertId, cancellationToken);
            ready.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return result;
        }
        public Task<Booking?> GetByIdAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken) =>
            inner.GetByIdAsync(bookingId, userId, cancellationToken);
        public void Add(Booking booking) => inner.Add(booking);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => inner.SaveChangesAsync(cancellationToken);
        public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken) => inner.BeginTransactionAsync(cancellationToken);
    }
}

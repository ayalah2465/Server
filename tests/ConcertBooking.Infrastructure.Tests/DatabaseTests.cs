using ConcertBooking.Domain.Entities;
using ConcertBooking.Domain.Enums;
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

        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
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

    private static User NewUser() => new()
    {
        Email = "test@example.invalid", NormalizedEmail = "TEST@EXAMPLE.INVALID",
        DisplayName = "Test User", PasswordHash = "TEST-ONLY-NOT-A-LOGIN-HASH"
    };
}

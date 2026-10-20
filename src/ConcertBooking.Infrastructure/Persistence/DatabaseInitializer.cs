using ConcertBooking.Domain.Entities;
using ConcertBooking.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ConcertBooking.Infrastructure.Persistence;

public sealed class DatabaseInitializer(AppDbContext dbContext)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        // Serialize simultaneous seed runs without changing runtime booking behavior.
        await dbContext.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(71306201)", cancellationToken);

        var names = new[] { "Piano", "Violin", "Flute" };
        var instruments = await dbContext.Instruments
            .Where(instrument => names.Contains(instrument.Name)).ToListAsync(cancellationToken);
        foreach (var name in names)
        {
            if (instruments.All(instrument => instrument.Name != name))
            {
                var instrument = new Instrument { Name = name };
                instruments.Add(instrument);
                dbContext.Instruments.Add(instrument);
            }
        }

        var demoId = Guid.Parse("b7f97ea0-0cf0-45f8-bc7e-7d381b707c01");
        if (!await dbContext.Concerts.AnyAsync(concert => concert.Id == demoId, cancellationToken))
        {
            dbContext.Concerts.Add(new Concert
            {
                Id = demoId,
                Title = "Evening of Chamber Music",
                Description = "Demo concert with piano, violin and flute. Payments are simulated only.",
                Venue = "Demo Music Hall",
                StartsAtUtc = DateTime.UtcNow.Date.AddDays(30).AddHours(18),
                Capacity = 30,
                TicketPrice = 75.00m,
                Instruments = instruments
            });
        }

        await AddDemoUserIfMissingAsync(
            "user@concert.local", "Demo User", "DemoUser123!", UserRole.User, cancellationToken);
        await AddDemoUserIfMissingAsync(
            "admin@concert.local", "Demo Admin", "DemoAdmin123!", UserRole.Admin, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task AddDemoUserIfMissingAsync(
        string email,
        string displayName,
        string publicDemoPassword,
        UserRole role,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.ToUpperInvariant();
        if (await dbContext.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken))
            return;

        var user = new User
        {
            Email = email,
            NormalizedEmail = normalizedEmail,
            DisplayName = displayName,
            Role = role
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, publicDemoPassword);
        dbContext.Users.Add(user);
    }
}

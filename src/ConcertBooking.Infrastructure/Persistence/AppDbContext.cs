using ConcertBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConcertBooking.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Concert> Concerts => Set<Concert>();
    public DbSet<Instrument> Instruments => Set<Instrument>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Concert>().Where(entry => entry.State == EntityState.Modified))
            entry.Entity.Version = Guid.NewGuid();

        return base.SaveChangesAsync(cancellationToken);
    }
}

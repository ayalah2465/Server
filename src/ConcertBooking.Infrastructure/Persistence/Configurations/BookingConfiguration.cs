using ConcertBooking.Domain.Entities;
using ConcertBooking.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConcertBooking.Infrastructure.Persistence.Configurations;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.HasKey(booking => booking.Id);
        builder.Property(booking => booking.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(booking => booking.TicketPrice).HasPrecision(10, 2);
        builder.HasOne(booking => booking.User).WithMany(user => user.Bookings)
            .HasForeignKey(booking => booking.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(booking => booking.Concert).WithMany(concert => concert.Bookings)
            .HasForeignKey(booking => booking.ConcertId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(booking => new { booking.UserId, booking.ConcertId })
            .IsUnique().HasFilter($"\"Status\" = {(int)BookingStatus.Confirmed}");
        builder.ToTable("Bookings", table =>
        {
            table.HasCheckConstraint("CK_Bookings_TicketPrice", "\"TicketPrice\" >= 0");
            table.HasCheckConstraint("CK_Bookings_Status", "\"Status\" IN (0, 1)");
        });
    }
}

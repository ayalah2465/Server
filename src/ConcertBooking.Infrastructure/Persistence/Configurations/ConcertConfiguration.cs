using ConcertBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConcertBooking.Infrastructure.Persistence.Configurations;

internal sealed class ConcertConfiguration : IEntityTypeConfiguration<Concert>
{
    public void Configure(EntityTypeBuilder<Concert> builder)
    {
        builder.HasKey(concert => concert.Id);
        builder.Property(concert => concert.Title).HasMaxLength(160).IsRequired();
        builder.Property(concert => concert.Description).HasMaxLength(2000).IsRequired();
        builder.Property(concert => concert.Venue).HasMaxLength(200).IsRequired();
        builder.Property(concert => concert.StartsAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(concert => concert.TicketPrice).HasPrecision(10, 2);
        builder.Property(concert => concert.Version).IsConcurrencyToken();
        builder.HasIndex(concert => concert.StartsAtUtc);
        builder.ToTable("Concerts", table =>
        {
            table.HasCheckConstraint("CK_Concerts_Capacity", "\"Capacity\" > 0");
            table.HasCheckConstraint("CK_Concerts_BookedSeats", "\"BookedSeats\" >= 0 AND \"BookedSeats\" <= \"Capacity\"");
            table.HasCheckConstraint("CK_Concerts_TicketPrice", "\"TicketPrice\" >= 0");
        });
        builder.HasMany(concert => concert.Instruments)
            .WithMany(instrument => instrument.Concerts)
            .UsingEntity<Dictionary<string, object>>(
                "ConcertInstruments",
                join => join.HasOne<Instrument>().WithMany().HasForeignKey("InstrumentId").OnDelete(DeleteBehavior.Restrict),
                join => join.HasOne<Concert>().WithMany().HasForeignKey("ConcertId").OnDelete(DeleteBehavior.Cascade),
                join => join.HasKey("ConcertId", "InstrumentId"));
    }
}

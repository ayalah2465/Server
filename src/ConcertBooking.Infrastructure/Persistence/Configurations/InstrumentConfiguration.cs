using ConcertBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConcertBooking.Infrastructure.Persistence.Configurations;

internal sealed class InstrumentConfiguration : IEntityTypeConfiguration<Instrument>
{
    public void Configure(EntityTypeBuilder<Instrument> builder)
    {
        builder.HasKey(instrument => instrument.Id);
        builder.Property(instrument => instrument.Name).HasMaxLength(80).IsRequired();
        builder.HasIndex(instrument => instrument.Name).IsUnique();
    }
}

using ConcertBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConcertBooking.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Email).HasMaxLength(254).IsRequired();
        builder.Property(user => user.NormalizedEmail).HasMaxLength(254).IsRequired();
        builder.Property(user => user.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(user => user.PasswordHash).HasMaxLength(1000).IsRequired();
        builder.HasIndex(user => user.NormalizedEmail).IsUnique();
        builder.ToTable("Users", table => table.HasCheckConstraint("CK_Users_Role", "\"Role\" IN (0, 1)"));
    }
}

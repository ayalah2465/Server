using ConcertBooking.Application.Authentication;
using ConcertBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ConcertBooking.Infrastructure.Persistence;

public sealed class UserRepository(AppDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

    public void Add(User user) => dbContext.Users.Add(user);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Users_NormalizedEmail"
        })
        {
            throw new DuplicateEmailException("A user with this email already exists.", exception);
        }
    }
}

using ConcertBooking.Domain.Entities;

namespace ConcertBooking.Application.Authentication;

public interface IUserRepository
{
    Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    void Add(User user);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

using ConcertBooking.Domain.Entities;

namespace ConcertBooking.Application.Authentication;

public interface IPasswordService
{
    string Hash(User user, string password);
    bool Verify(User user, string passwordHash, string password);
}

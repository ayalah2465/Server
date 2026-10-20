using ConcertBooking.Domain.Entities;

namespace ConcertBooking.Application.Authentication;

public interface ITokenService
{
    AccessToken Create(User user);
}

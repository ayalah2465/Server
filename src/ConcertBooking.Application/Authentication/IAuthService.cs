namespace ConcertBooking.Application.Authentication;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string email, string displayName, string password, CancellationToken cancellationToken = default);
    Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
}

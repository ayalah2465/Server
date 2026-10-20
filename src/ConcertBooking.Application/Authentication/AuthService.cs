using ConcertBooking.Domain.Entities;

namespace ConcertBooking.Application.Authentication;

public sealed class AuthService(
    IUserRepository users,
    IPasswordService passwords,
    ITokenService tokens) : IAuthService
{
    public async Task<AuthResult> RegisterAsync(
        string email,
        string displayName,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (await users.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken) is not null)
            return AuthResult.Rejected(AuthFailure.DuplicateEmail);

        var user = new User
        {
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            DisplayName = displayName.Trim()
        };
        user.PasswordHash = passwords.Hash(user, password);
        users.Add(user);

        try
        {
            await users.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateEmailException)
        {
            return AuthResult.Rejected(AuthFailure.DuplicateEmail);
        }

        return AuthResult.Success(tokens.Create(user));
    }

    public async Task<AuthResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await users.GetByNormalizedEmailAsync(NormalizeEmail(email), cancellationToken);
        if (user is null || !passwords.Verify(user, user.PasswordHash, password))
            return AuthResult.Rejected(AuthFailure.InvalidCredentials);

        return AuthResult.Success(tokens.Create(user));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
}

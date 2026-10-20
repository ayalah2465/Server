namespace ConcertBooking.Application.Authentication;

public sealed record AuthResult(string? AccessToken, DateTime? ExpiresAtUtc, AuthFailure Failure)
{
    public bool IsSuccess => Failure == AuthFailure.None;
    public static AuthResult Success(AccessToken token) => new(token.Value, token.ExpiresAtUtc, AuthFailure.None);
    public static AuthResult Rejected(AuthFailure failure) => new(null, null, failure);
}

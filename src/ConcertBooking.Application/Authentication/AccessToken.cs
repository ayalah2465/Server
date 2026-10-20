namespace ConcertBooking.Application.Authentication;

public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);

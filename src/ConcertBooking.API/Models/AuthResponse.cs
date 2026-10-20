namespace ConcertBooking.API.Models;

public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc);

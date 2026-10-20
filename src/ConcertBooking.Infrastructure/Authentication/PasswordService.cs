using ConcertBooking.Application.Authentication;
using ConcertBooking.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace ConcertBooking.Infrastructure.Authentication;

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<User> hasher = new();

    public string Hash(User user, string password) => hasher.HashPassword(user, password);

    public bool Verify(User user, string passwordHash, string password) =>
        hasher.VerifyHashedPassword(user, passwordHash, password) != PasswordVerificationResult.Failed;
}

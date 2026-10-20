using ConcertBooking.Application.Authentication;
using ConcertBooking.Domain.Entities;
using Moq;

namespace ConcertBooking.Application.Tests;

public sealed class AuthServiceTests
{
    private readonly Mock<IUserRepository> users = new(MockBehavior.Strict);
    private readonly Mock<IPasswordService> passwords = new(MockBehavior.Strict);
    private readonly Mock<ITokenService> tokens = new(MockBehavior.Strict);

    [Fact]
    public async Task Register_hashes_password_saves_user_and_returns_token()
    {
        User? added = null;
        users.Setup(item => item.GetByNormalizedEmailAsync("USER@EXAMPLE.COM", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        passwords.Setup(item => item.Hash(It.IsAny<User>(), "Secure123!")).Returns("HASH");
        users.Setup(item => item.Add(It.IsAny<User>())).Callback<User>(user => added = user);
        users.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        tokens.Setup(item => item.Create(It.IsAny<User>()))
            .Returns(new AccessToken("token", new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        var result = await CreateService().RegisterAsync(" user@example.com ", " Demo User ", "Secure123!");

        Assert.True(result.IsSuccess);
        Assert.Equal("token", result.AccessToken);
        Assert.Equal("user@example.com", added!.Email);
        Assert.Equal("USER@EXAMPLE.COM", added.NormalizedEmail);
        Assert.Equal("Demo User", added.DisplayName);
        Assert.Equal("HASH", added.PasswordHash);
    }

    [Fact]
    public async Task Register_rejects_existing_email_without_hashing_or_saving()
    {
        users.Setup(item => item.GetByNormalizedEmailAsync("USER@EXAMPLE.COM", It.IsAny<CancellationToken>())).ReturnsAsync(new User());

        var result = await CreateService().RegisterAsync("user@example.com", "User", "Secure123!");

        Assert.Equal(AuthFailure.DuplicateEmail, result.Failure);
        passwords.VerifyNoOtherCalls();
        tokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Register_translates_concurrent_duplicate_email()
    {
        users.Setup(item => item.GetByNormalizedEmailAsync("USER@EXAMPLE.COM", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        passwords.Setup(item => item.Hash(It.IsAny<User>(), "Secure123!")).Returns("HASH");
        users.Setup(item => item.Add(It.IsAny<User>()));
        users.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new DuplicateEmailException("duplicate"));

        var result = await CreateService().RegisterAsync("user@example.com", "User", "Secure123!");

        Assert.Equal(AuthFailure.DuplicateEmail, result.Failure);
        tokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Login_rejects_missing_user_and_wrong_password_with_same_failure()
    {
        users.SetupSequence(item => item.GetByNormalizedEmailAsync("USER@EXAMPLE.COM", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null)
            .ReturnsAsync(new User { PasswordHash = "HASH" });
        passwords.Setup(item => item.Verify(It.IsAny<User>(), "HASH", "Wrong123!")).Returns(false);

        var missing = await CreateService().LoginAsync("user@example.com", "Wrong123!");
        var wrong = await CreateService().LoginAsync("user@example.com", "Wrong123!");

        Assert.Equal(AuthFailure.InvalidCredentials, missing.Failure);
        Assert.Equal(AuthFailure.InvalidCredentials, wrong.Failure);
        tokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Login_returns_token_for_valid_password()
    {
        var user = new User { PasswordHash = "HASH" };
        users.Setup(item => item.GetByNormalizedEmailAsync("USER@EXAMPLE.COM", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        passwords.Setup(item => item.Verify(user, "HASH", "Secure123!")).Returns(true);
        tokens.Setup(item => item.Create(user))
            .Returns(new AccessToken("token", new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        var result = await CreateService().LoginAsync("user@example.com", "Secure123!");

        Assert.True(result.IsSuccess);
        Assert.Equal("token", result.AccessToken);
    }

    private AuthService CreateService() => new(users.Object, passwords.Object, tokens.Object);
}

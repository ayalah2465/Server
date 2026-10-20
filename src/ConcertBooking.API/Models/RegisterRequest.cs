using System.ComponentModel.DataAnnotations;

namespace ConcertBooking.API.Models;

public sealed class RegisterRequest
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string DisplayName { get; init; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;
}

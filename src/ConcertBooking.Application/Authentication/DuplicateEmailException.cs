namespace ConcertBooking.Application.Authentication;

public sealed class DuplicateEmailException(string message, Exception? innerException = null)
    : Exception(message, innerException);

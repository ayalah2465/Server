namespace ConcertBooking.Application.Bookings;

public sealed class ConcurrencyConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);

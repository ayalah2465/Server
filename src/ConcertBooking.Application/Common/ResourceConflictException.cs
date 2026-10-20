namespace ConcertBooking.Application.Common;

public sealed class ResourceConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);

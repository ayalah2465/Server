namespace ConcertBooking.Application.Bookings;

public enum BookingFailure
{
    None,
    ConcertNotFound,
    UserNotFound,
    ConcertStarted,
    SoldOut,
    AlreadyBooked,
    Conflict
}

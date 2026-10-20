namespace ConcertBooking.Application.Bookings;

public sealed record BookingResult(Guid? BookingId, BookingFailure Failure)
{
    public bool IsSuccess => Failure == BookingFailure.None;

    public static BookingResult Success(Guid bookingId) => new(bookingId, BookingFailure.None);
    public static BookingResult Rejected(BookingFailure failure) => new(null, failure);
}

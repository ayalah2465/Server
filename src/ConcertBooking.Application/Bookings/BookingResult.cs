namespace ConcertBooking.Application.Bookings;

public sealed record BookingResult(Guid? BookingId, decimal? TicketPrice, BookingFailure Failure)
{
    public bool IsSuccess => Failure == BookingFailure.None;

    public static BookingResult Success(Guid bookingId, decimal ticketPrice) => new(bookingId, ticketPrice, BookingFailure.None);
    public static BookingResult Rejected(BookingFailure failure) => new(null, null, failure);
}

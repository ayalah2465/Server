using ConcertBooking.Domain.Enums;

namespace ConcertBooking.Domain.Entities;

public sealed class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid ConcertId { get; set; }
    public Concert Concert { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public decimal TicketPrice { get; set; }
}

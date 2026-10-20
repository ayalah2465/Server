namespace ConcertBooking.Domain.Entities;

public sealed class Concert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public DateTime StartsAtUtc { get; set; }
    public int Capacity { get; set; }
    public int BookedSeats { get; set; }
    public decimal TicketPrice { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public ICollection<Instrument> Instruments { get; set; } = new List<Instrument>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}

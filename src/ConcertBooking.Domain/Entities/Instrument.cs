namespace ConcertBooking.Domain.Entities;

public sealed class Instrument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public ICollection<Concert> Concerts { get; set; } = new List<Concert>();
}

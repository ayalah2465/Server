using System.ComponentModel.DataAnnotations;

namespace ConcertBooking.Application.Concerts;

public sealed class ConcertQuery
{
    [StringLength(160)] public string? Search { get; init; }
    [StringLength(80)] public string? Instrument { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    [RegularExpression("^(startsAt|title|price)$")] public string SortBy { get; init; } = "startsAt";
    [RegularExpression("^(asc|desc)$")] public string SortDirection { get; init; } = "asc";
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 10;
}

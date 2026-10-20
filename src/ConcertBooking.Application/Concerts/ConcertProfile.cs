using AutoMapper;
using ConcertBooking.Domain.Entities;

namespace ConcertBooking.Application.Concerts;

public sealed class ConcertProfile : Profile
{
    public ConcertProfile()
    {
        CreateMap<Instrument, InstrumentResponse>();
        CreateMap<Concert, ConcertResponse>()
            .ForCtorParam(nameof(ConcertResponse.RemainingSeats), options =>
                options.MapFrom(concert => concert.Capacity - concert.BookedSeats));
    }
}

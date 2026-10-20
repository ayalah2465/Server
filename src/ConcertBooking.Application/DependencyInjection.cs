using ConcertBooking.Application.Bookings;
using ConcertBooking.Application.Authentication;
using ConcertBooking.Application.Concerts;
using ConcertBooking.Application.Instruments;
using Microsoft.Extensions.DependencyInjection;

namespace ConcertBooking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IConcertService, ConcertService>();
        services.AddScoped<IInstrumentService, InstrumentService>();
        services.AddAutoMapper(configuration => { }, typeof(ConcertProfile).Assembly);
        return services;
    }
}

using ConcertBooking.Application.Bookings;
using ConcertBooking.Application.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace ConcertBooking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}

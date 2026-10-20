using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using ConcertBooking.API.Models;
using ConcertBooking.Application.Common;
using ConcertBooking.Application.Concerts;
using ConcertBooking.Infrastructure.Persistence;
using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace ConcertBooking.API.Tests;

public sealed class AuthenticationApiTests : IAsyncLifetime
{
    private const string Issuer = "ConcertBooking.Tests";
    private const string Audience = "ConcertBooking.TestClient";
    private const string SigningKey = "TEST-ONLY-signing-key-with-at-least-32-characters";
    private readonly string databaseName = $"concert_test_{Guid.NewGuid():N}";
    private string adminConnectionString = string.Empty;
    private ConcertApiFactory factory = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("CONCERT_TEST_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Set CONCERT_TEST_CONNECTION_STRING for API integration tests.");
        var builder = new NpgsqlConnectionStringBuilder(configured) { Database = "postgres", Pooling = false };
        adminConnectionString = builder.ConnectionString;
        await using (var connection = new NpgsqlConnection(adminConnectionString))
        {
            await connection.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection).ExecuteNonQueryAsync();
        }
        builder.Database = databaseName;
        factory = new ConcertApiFactory(builder.ConnectionString);
        client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        client?.Dispose();
        if (factory is not null) await factory.DisposeAsync();
        if (string.IsNullOrEmpty(adminConnectionString)) return;
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection).ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Registration_and_login_validate_input_credentials_and_duplicate_email()
    {
        var invalid = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "not-an-email", displayName = "A", password = "short"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "new.user@example.com", displayName = "New User", password = "Secure123!"
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var registered = await register.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(registered?.AccessToken));

        var duplicate = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "NEW.USER@example.com", displayName = "Duplicate", password = "Secure123!"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var invalidLogin = await LoginAsync("new.user@example.com", "Wrong123!");
        Assert.Equal(HttpStatusCode.Unauthorized, invalidLogin.StatusCode);
        var login = await LoginAsync("new.user@example.com", "Secure123!");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoints_enforce_authentication_and_roles()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/access/user")).StatusCode);

        var userToken = await ReadTokenAsync(await LoginAsync("user@concert.local", "DemoUser123!"));
        ValidateTokenLocally(userToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/access/user")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/access/admin")).StatusCode);

        var adminToken = await ReadTokenAsync(await LoginAsync("admin@concert.local", "DemoAdmin123!"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/access/admin")).StatusCode);
    }

    [Fact]
    public async Task Invalid_and_expired_tokens_are_rejected()
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/access/user")).StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateExpiredToken());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/access/user")).StatusCode);
    }

    [Fact]
    public async Task Admin_crud_and_public_query_support_mapping_filter_sort_and_database_pagination()
    {
        await SetAuthorizationAsync("admin@concert.local", "DemoAdmin123!");
        using (var scope = factory.Services.CreateScope())
            scope.ServiceProvider.GetRequiredService<IMapper>().ConfigurationProvider.AssertConfigurationIsValid();

        var instruments = await client.GetFromJsonAsync<List<InstrumentResponse>>("/api/instruments");
        var piano = Assert.Single(instruments!, item => item.Name == "Piano");
        var create = await client.PostAsJsonAsync("/api/concerts", new
        {
            title = "Pagination Test Concert", description = "Created by API integration test",
            venue = "Test Hall", startsAtUtc = DateTime.UtcNow.AddDays(20), capacity = 4,
            ticketPrice = 50.25m, instrumentIds = new[] { piano.Id }
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(create.Headers.Location);
        var concert = await create.Content.ReadFromJsonAsync<ConcertResponse>();
        Assert.Equal(4, concert!.RemainingSeats);

        var page = await client.GetFromJsonAsync<PagedResult<ConcertResponse>>(
            "/api/concerts?search=Pagination&instrument=Piano&sortBy=price&sortDirection=desc&page=1&pageSize=1");
        Assert.Equal(1, page!.PageSize);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(concert.Id, Assert.Single(page.Items).Id);

        var update = await client.PutAsJsonAsync($"/api/concerts/{concert.Id}", new
        {
            title = "Updated Concert", description = "Updated", venue = "Updated Hall",
            startsAtUtc = DateTime.UtcNow.AddDays(21), capacity = 5, ticketPrice = 60m,
            instrumentIds = new[] { piano.Id }
        });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        Assert.Equal("Updated Concert", (await client.GetFromJsonAsync<ConcertResponse>($"/api/concerts/{concert.Id}"))!.Title);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/concerts/{concert.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/concerts/{concert.Id}")).StatusCode);

        var createInstrument = await client.PostAsJsonAsync("/api/instruments", new { name = "Cello" });
        Assert.Equal(HttpStatusCode.Created, createInstrument.StatusCode);
        var cello = await createInstrument.Content.ReadFromJsonAsync<InstrumentResponse>();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/instruments", new { name = "cello" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/instruments/{cello!.Id}", new { name = "Double Bass" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/instruments/{cello.Id}")).StatusCode);
    }

    [Fact]
    public async Task Booking_requires_authentication_and_approved_simulated_payment_and_prevents_duplicates()
    {
        var concertPage = await client.GetFromJsonAsync<PagedResult<ConcertResponse>>("/api/concerts?pageSize=10");
        var concert = Assert.Single(concertPage!.Items);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync($"/api/concerts/{concert.Id}/bookings", new { approved = true })).StatusCode);

        await SetAuthorizationAsync("user@concert.local", "DemoUser123!");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/concerts/{concert.Id}/bookings", new { approved = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync($"/api/concerts/{Guid.NewGuid()}/bookings", new { approved = true })).StatusCode);

        var booked = await client.PostAsJsonAsync($"/api/concerts/{concert.Id}/bookings", new { approved = true });
        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        Assert.NotNull(booked.Headers.Location);
        Assert.Equal("SimulatedApproved", (await booked.Content.ReadFromJsonAsync<ConcertBooking.Application.Bookings.BookingDetails>())!.PaymentStatus);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(booked.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync($"/api/concerts/{concert.Id}/bookings", new { approved = true })).StatusCode);

        await SetAuthorizationAsync("admin@concert.local", "DemoAdmin123!");
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/concerts/{concert.Id}")).StatusCode);
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });

    private async Task SetAuthorizationAsync(string email, string password)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await ReadTokenAsync(await LoginAsync(email, password)));
    }

    private static async Task<string> ReadTokenAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return Assert.IsType<string>(body?.AccessToken);
    }

    private static string CreateExpiredToken()
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            Issuer,
            Audience,
            new[] { new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()) },
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: DateTime.UtcNow.AddHours(-1),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static void ValidateTokenLocally(string token)
    {
        new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        }, out _);
    }

    private sealed class ConcertApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = connectionString,
                    ["Jwt:Issuer"] = Issuer,
                    ["Jwt:Audience"] = Audience,
                    ["Jwt:SigningKey"] = SigningKey,
                    ["Jwt:ExpirationMinutes"] = "60"
                }));
        }
    }
}

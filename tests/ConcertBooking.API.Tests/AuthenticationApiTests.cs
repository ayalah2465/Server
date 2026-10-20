using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using ConcertBooking.API.Models;
using ConcertBooking.Infrastructure.Persistence;
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

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });

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

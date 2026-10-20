# ConcertBooking

Student project: concert registration with limited capacity, instrument categories,
ticket prices, and clearly labelled simulated payment. No card details or real payments.

## Current stage

Database stage complete: .NET 8, Domain/Application/Infrastructure/API layers,
xUnit/Moq architecture tests, and PostgreSQL integration tests. Entities, Fluent API
relationships, the first migration and repeatable demo seed data are implemented.
Booking operations, concurrency handling, authentication, payment and client behavior
are not implemented yet; direct database constraints do not replace those workflows.
The solution uses Clean Architecture: Domain has no external dependencies;
Application references Domain; Infrastructure references Application; API references
Application and Infrastructure. API uses Infrastructure only for dependency registration.
Business use cases and interfaces belong in Application; persistence implementations
belong in Infrastructure. Controllers must not access DbContext directly.

## Development

Install the SDK selected in `global.json` and Docker Compose. From the repository
root, copy `.env.example` to `.env` and choose a local database password there.
The Compose file binds PostgreSQL to loopback only and retains data in a named volume.
Do not use this development database role or these credentials in production.

```sh
docker compose up -d --wait
dotnet tool restore
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=concert_booking;Username=concert_dev;Password=<the-local-password>" --project src/ConcertBooking.API
```

Replace the placeholder locally. Do not share or commit the actual connection string.
In development the API reads User Secrets; deployment can inject the configuration
key `ConnectionStrings__Default`. No connection string is committed in appsettings.

Build and initialize from the repository root (Bash syntax):

```sh
dotnet restore ConcertBooking.sln
dotnet build ConcertBooking.sln --no-restore
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/ConcertBooking.API --no-build -- --initialize-database
dotnet run --project src/ConcertBooking.API --no-launch-profile
```

For development startup and the OpenAPI check, set `ASPNETCORE_ENVIRONMENT=Development` and
`ASPNETCORE_URLS=http://127.0.0.1:5080`, then request `/openapi/v1.json`.
Swagger UI is available at `/swagger` in development. Database initialization applies
the committed migrations and creates one demo concert linked to Piano, Violin and
Flute. Repeating it preserves edits and creates no duplicate demo data. Concert dates
are generated in UTC on first initialization. Demo login accounts will be added in
the authentication stage; none are currently seeded.

### Tests

```sh
dotnet test tests/ConcertBooking.Application.Tests --no-build
```

For the complete suite, securely set `CONCERT_TEST_CONNECTION_STRING` to a local
PostgreSQL connection string whose role can create databases, then run:

```sh
dotnet test ConcertBooking.sln --no-build
```

The integration tests require real PostgreSQL and deliberately fail when this variable
is absent. Each test creates and drops its own randomly named `concert_test_*` database;
the configured application database is not reset. Ten integration cases verify fresh
migrations, model consistency, repeatable seed data, relationships, UTC reads, and
database constraints. Five architecture cases verify dependency boundaries.

To add a later migration, use the pinned local EF tool:

```sh
ASPNETCORE_ENVIRONMENT=Development dotnet ef migrations add <MigrationName> --project src/ConcertBooking.Infrastructure --startup-project src/ConcertBooking.API --output-dir Persistence/Migrations
```

Secrets, build outputs and local databases must not enter Git. `docker compose down`
stops the database and retains its volume; avoid `down -v` unless deletion is intended.

See [the bilingual plan](PLAN.md). Each server stage requires tests and approval
before the next stage. The client stage has no intermediate approval gates.

$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

foreach ($command in @("docker", "dotnet")) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "Required command not found: $command"
    }
}

$securePassword = Read-Host "Choose a local PostgreSQL password (12+ letters, digits, . _ or -)" -AsSecureString
$passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
try {
    $postgresPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
}
if ($postgresPassword -notmatch '^[A-Za-z0-9._-]{12,}$') {
    throw "The password does not meet the local setup requirements."
}

@"
POSTGRES_USER=concert_dev
POSTGRES_DB=concert_booking
POSTGRES_PASSWORD=$postgresPassword
POSTGRES_PORT=5432
"@ | Set-Content -Encoding utf8NoBOM .env

docker compose up -d --wait
dotnet tool restore
dotnet restore ConcertBooking.sln

$randomBytes = New-Object byte[] 48
[Security.Cryptography.RandomNumberGenerator]::Fill($randomBytes)
$jwtKey = [Convert]::ToBase64String($randomBytes)
$connectionString = "Host=localhost;Port=5432;Database=concert_booking;Username=concert_dev;Password=$postgresPassword"
dotnet user-secrets set "ConnectionStrings:Default" $connectionString --project src/ConcertBooking.API | Out-Null
dotnet user-secrets set "Jwt:SigningKey" $jwtKey --project src/ConcertBooking.API | Out-Null
dotnet build ConcertBooking.sln --no-restore
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/ConcertBooking.API --no-build -- --initialize-database

$env:CONCERT_TEST_CONNECTION_STRING = "Host=localhost;Port=5432;Database=postgres;Username=concert_dev;Password=$postgresPassword"
dotnet test ConcertBooking.sln --no-build
Remove-Item Env:CONCERT_TEST_CONNECTION_STRING

Write-Host "Setup succeeded. Open ConcertBooking.sln in Visual Studio or run:"
Write-Host "dotnet run --project src/ConcertBooking.API --launch-profile http"

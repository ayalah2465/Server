#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

for command in docker dotnet; do
  if ! command -v "$command" >/dev/null 2>&1; then
    echo "Required command not found: $command" >&2
    exit 1
  fi
done

read -r -s -p "Choose a local PostgreSQL password (12+ letters, digits, . _ or -): " postgres_password
echo
if [[ ! "$postgres_password" =~ ^[A-Za-z0-9._-]{12,}$ ]]; then
  echo "The password does not meet the local setup requirements." >&2
  exit 1
fi

umask 077
cat > .env <<EOF
POSTGRES_USER=concert_dev
POSTGRES_DB=concert_booking
POSTGRES_PASSWORD=$postgres_password
POSTGRES_PORT=5432
EOF

docker compose up -d --wait
dotnet tool restore
dotnet restore ConcertBooking.sln

jwt_key="$(openssl rand -base64 48 2>/dev/null || head -c 48 /dev/urandom | base64)"
connection_string="Host=localhost;Port=5432;Database=concert_booking;Username=concert_dev;Password=$postgres_password"
dotnet user-secrets set "ConnectionStrings:Default" "$connection_string" --project src/ConcertBooking.API >/dev/null
dotnet user-secrets set "Jwt:SigningKey" "$jwt_key" --project src/ConcertBooking.API >/dev/null
dotnet build ConcertBooking.sln --no-restore
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/ConcertBooking.API --no-build -- --initialize-database

export CONCERT_TEST_CONNECTION_STRING="Host=localhost;Port=5432;Database=postgres;Username=concert_dev;Password=$postgres_password"
dotnet test ConcertBooking.sln --no-build

echo "Setup succeeded. Open ConcertBooking.sln in Visual Studio or run:"
echo "dotnet run --project src/ConcertBooking.API --launch-profile http"

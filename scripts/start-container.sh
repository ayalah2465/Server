#!/usr/bin/env sh
set -eu

export ASPNETCORE_URLS="http://+:${PORT:-8080}"
dotnet ConcertBooking.API.dll --initialize-database
exec dotnet ConcertBooking.API.dll

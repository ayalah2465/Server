FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore ConcertBooking.sln \
    && dotnet publish src/ConcertBooking.API/ConcertBooking.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app .
COPY scripts/start-container.sh /app/start-container.sh
RUN chmod +x /app/start-container.sh
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["/app/start-container.sh"]

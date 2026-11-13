# Multi-stage Dockerfile for Ledgr API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Copy solution and project files first for efficient caching
COPY Directory.Build.props .
COPY Ledgr.sln .
COPY src/Ledgr.Api/Ledgr.Api.csproj src/Ledgr.Api/
COPY tests/Ledgr.Tests/Ledgr.Tests.csproj tests/Ledgr.Tests/

RUN dotnet restore

# Copy all source files and publish
COPY . .
WORKDIR /source/src/Ledgr.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Non-root user for security best practice
USER app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV Database__ConnectionString="Data Source=/app/data/ledgr.db"

EXPOSE 8080

ENTRYPOINT ["dotnet", "Ledgr.Api.dll"]

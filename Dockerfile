# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy all project files for dependency caching
COPY sources/SafeTravel.Domain/SafeTravel.Domain.csproj SafeTravel.Domain/
COPY sources/SafeTravel.Application/SafeTravel.Application.csproj SafeTravel.Application/
COPY sources/SafeTravel.Infrastructure/SafeTravel.Infrastructure.csproj SafeTravel.Infrastructure/
COPY sources/SafeTravel.Api/SafeTravel.Api.csproj SafeTravel.Api/

# Restore dependencies
RUN dotnet restore SafeTravel.Api/SafeTravel.Api.csproj

# Copy all source code
COPY sources/ .

# Build and publish
RUN dotnet publish SafeTravel.Api/SafeTravel.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Create non-root user for security
RUN groupadd -r appgroup && useradd -r -g appgroup appuser

WORKDIR /app

# Copy published files
COPY --from=build /app/publish .

# Set ownership to non-root user
RUN chown -R appuser:appgroup /app

# Switch to non-root user
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl --fail http://localhost:8080/health || exit 1

EXPOSE 8080

ENTRYPOINT ["dotnet", "SafeTravel.Api.dll"]


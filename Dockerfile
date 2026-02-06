# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project files first for layer caching
COPY sources/SafeTravel.Domain/SafeTravel.Domain.csproj SafeTravel.Domain/
COPY sources/SafeTravel.Application/SafeTravel.Application.csproj SafeTravel.Application/
COPY sources/SafeTravel.Infrastructure/SafeTravel.Infrastructure.csproj SafeTravel.Infrastructure/
COPY sources/SafeTravel.Api/SafeTravel.Api.csproj SafeTravel.Api/

# Restore dependencies
RUN dotnet restore SafeTravel.Api/SafeTravel.Api.csproj

# Copy source code
COPY sources/SafeTravel.Domain/ SafeTravel.Domain/
COPY sources/SafeTravel.Application/ SafeTravel.Application/
COPY sources/SafeTravel.Infrastructure/ SafeTravel.Infrastructure/
COPY sources/SafeTravel.Api/ SafeTravel.Api/

# Build and publish
RUN dotnet publish SafeTravel.Api/SafeTravel.Api.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SafeTravel.Api.dll"]


# ==============================================================================
# Multi-stage Dockerfile for GuardianDigital.Api (.NET 10)
# Optimized for Render.com Web Services & Container Deployments
# ==============================================================================

# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first to leverage Docker layer caching on restores
COPY ["src/GuardianDigital.Domain/GuardianDigital.Domain.csproj", "src/GuardianDigital.Domain/"]
COPY ["src/GuardianDigital.Application/GuardianDigital.Application.csproj", "src/GuardianDigital.Application/"]
COPY ["src/GuardianDigital.Infrastructure/GuardianDigital.Infrastructure.csproj", "src/GuardianDigital.Infrastructure/"]
COPY ["src/GuardianDigital.Api/GuardianDigital.Api.csproj", "src/GuardianDigital.Api/"]

# Restore NuGet dependencies
RUN dotnet restore "src/GuardianDigital.Api/GuardianDigital.Api.csproj"

# Copy all source files
COPY src/ src/

# Build and publish optimized release bundle
WORKDIR "/src/src/GuardianDigital.Api"
RUN dotnet publish "GuardianDigital.Api.csproj" -c Release -o /app/publish --no-restore

# Stage 2: Runtime Image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Default ASP.NET Core environment for container deployments (overridden dynamically by Render $PORT)
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Copy published application from build stage
COPY --from=build /app/publish .

# Expose default HTTP port
EXPOSE 8080

# Run API application
ENTRYPOINT ["dotnet", "GuardianDigital.Api.dll"]

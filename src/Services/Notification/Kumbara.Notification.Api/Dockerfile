# =============================================================================
# Kumbara Notification API - Dockerfile (.NET 8)
# Build Context: repository root
# =============================================================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy BuildingBlocks csproj files for restore caching
COPY ["src/BuildingBlocks/Kumbara.BuildingBlocks.Contracts/Kumbara.BuildingBlocks.Contracts.csproj", "src/BuildingBlocks/Kumbara.BuildingBlocks.Contracts/"]

# Copy Notification service csproj files
COPY ["src/Services/Notification/Kumbara.Notification.Domain/Kumbara.Notification.Domain.csproj", "src/Services/Notification/Kumbara.Notification.Domain/"]
COPY ["src/Services/Notification/Kumbara.Notification.Application/Kumbara.Notification.Application.csproj", "src/Services/Notification/Kumbara.Notification.Application/"]
COPY ["src/Services/Notification/Kumbara.Notification.Infrastructure/Kumbara.Notification.Infrastructure.csproj", "src/Services/Notification/Kumbara.Notification.Infrastructure/"]
COPY ["src/Services/Notification/Kumbara.Notification.Api/Kumbara.Notification.Api.csproj", "src/Services/Notification/Kumbara.Notification.Api/"]

# Restore dependencies
RUN dotnet restore "src/Services/Notification/Kumbara.Notification.Api/Kumbara.Notification.Api.csproj"

# Copy source trees
COPY ["src/BuildingBlocks/Kumbara.BuildingBlocks.Contracts/", "src/BuildingBlocks/Kumbara.BuildingBlocks.Contracts/"]
COPY ["src/Services/Notification/", "src/Services/Notification/"]

# Publish in Release mode
WORKDIR "/src/src/Services/Notification/Kumbara.Notification.Api"
RUN dotnet publish "Kumbara.Notification.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Kumbara.Notification.Api.dll"]

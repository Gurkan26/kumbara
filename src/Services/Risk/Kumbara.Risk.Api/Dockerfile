# =============================================================================
# Kumbara Risk API - Dockerfile (.NET 8)
# Build Context: repository root
# =============================================================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy BuildingBlocks csproj files for restore caching
COPY ["src/BuildingBlocks/Kumbara.BuildingBlocks.Contracts/Kumbara.BuildingBlocks.Contracts.csproj", "src/BuildingBlocks/Kumbara.BuildingBlocks.Contracts/"]
COPY ["src/BuildingBlocks/Kumbara.BuildingBlocks.Outbox/Kumbara.BuildingBlocks.Outbox.csproj", "src/BuildingBlocks/Kumbara.BuildingBlocks.Outbox/"]
COPY ["src/BuildingBlocks/Kumbara.BuildingBlocks.Idempotency/Kumbara.BuildingBlocks.Idempotency.csproj", "src/BuildingBlocks/Kumbara.BuildingBlocks.Idempotency/"]

# Copy Risk service csproj files
COPY ["src/Services/Risk/Kumbara.Risk.Domain/Kumbara.Risk.Domain.csproj", "src/Services/Risk/Kumbara.Risk.Domain/"]
COPY ["src/Services/Risk/Kumbara.Risk.Application/Kumbara.Risk.Application.csproj", "src/Services/Risk/Kumbara.Risk.Application/"]
COPY ["src/Services/Risk/Kumbara.Risk.Infrastructure/Kumbara.Risk.Infrastructure.csproj", "src/Services/Risk/Kumbara.Risk.Infrastructure/"]
COPY ["src/Services/Risk/Kumbara.Risk.Api/Kumbara.Risk.Api.csproj", "src/Services/Risk/Kumbara.Risk.Api/"]

# Restore dependencies
RUN dotnet restore "src/Services/Risk/Kumbara.Risk.Api/Kumbara.Risk.Api.csproj"

# Copy source trees
COPY ["src/BuildingBlocks/", "src/BuildingBlocks/"]
COPY ["src/Services/Risk/", "src/Services/Risk/"]

# Publish in Release mode
WORKDIR "/src/src/Services/Risk/Kumbara.Risk.Api"
RUN dotnet publish "Kumbara.Risk.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Kumbara.Risk.Api.dll"]

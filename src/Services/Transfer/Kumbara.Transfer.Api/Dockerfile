# =============================================================================
# Kumbara Transfer API - Dockerfile (.NET 8)
# Build Context: repository root
# =============================================================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy BuildingBlocks csproj files for restore caching
COPY ["src/BuildingBlocks/Kumbara.BuildingBlocks.Contracts/Kumbara.BuildingBlocks.Contracts.csproj", "src/BuildingBlocks/Kumbara.BuildingBlocks.Contracts/"]
COPY ["src/BuildingBlocks/Kumbara.BuildingBlocks.Outbox/Kumbara.BuildingBlocks.Outbox.csproj", "src/BuildingBlocks/Kumbara.BuildingBlocks.Outbox/"]
COPY ["src/BuildingBlocks/Kumbara.BuildingBlocks.Idempotency/Kumbara.BuildingBlocks.Idempotency.csproj", "src/BuildingBlocks/Kumbara.BuildingBlocks.Idempotency/"]

# Copy Transfer service csproj files
COPY ["src/Services/Transfer/Kumbara.Transfer.Domain/Kumbara.Transfer.Domain.csproj", "src/Services/Transfer/Kumbara.Transfer.Domain/"]
COPY ["src/Services/Transfer/Kumbara.Transfer.Application/Kumbara.Transfer.Application.csproj", "src/Services/Transfer/Kumbara.Transfer.Application/"]
COPY ["src/Services/Transfer/Kumbara.Transfer.Infrastructure/Kumbara.Transfer.Infrastructure.csproj", "src/Services/Transfer/Kumbara.Transfer.Infrastructure/"]
COPY ["src/Services/Transfer/Kumbara.Transfer.Api/Kumbara.Transfer.Api.csproj", "src/Services/Transfer/Kumbara.Transfer.Api/"]

# Restore dependencies
RUN dotnet restore "src/Services/Transfer/Kumbara.Transfer.Api/Kumbara.Transfer.Api.csproj"

# Copy source trees
COPY ["src/BuildingBlocks/", "src/BuildingBlocks/"]
COPY ["src/Services/Transfer/", "src/Services/Transfer/"]

# Publish in Release mode
WORKDIR "/src/src/Services/Transfer/Kumbara.Transfer.Api"
RUN dotnet publish "Kumbara.Transfer.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Kumbara.Transfer.Api.dll"]

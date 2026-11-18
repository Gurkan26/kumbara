# =============================================================================
# Kumbara Identity API - Dockerfile (.NET 8)
# Build Context: repository root
# =============================================================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy BuildingBlocks csproj files for restore caching
COPY ["src/BuildingBlocks/Kumbara.BuildingBlocks.Contracts/Kumbara.BuildingBlocks.Contracts.csproj", "src/BuildingBlocks/Kumbara.BuildingBlocks.Contracts/"]
COPY ["src/BuildingBlocks/Kumbara.BuildingBlocks.Outbox/Kumbara.BuildingBlocks.Outbox.csproj", "src/BuildingBlocks/Kumbara.BuildingBlocks.Outbox/"]
COPY ["src/BuildingBlocks/Kumbara.BuildingBlocks.Idempotency/Kumbara.BuildingBlocks.Idempotency.csproj", "src/BuildingBlocks/Kumbara.BuildingBlocks.Idempotency/"]

# Copy Identity service csproj files
COPY ["src/Services/Identity/Kumbara.Identity.Domain/Kumbara.Identity.Domain.csproj", "src/Services/Identity/Kumbara.Identity.Domain/"]
COPY ["src/Services/Identity/Kumbara.Identity.Application/Kumbara.Identity.Application.csproj", "src/Services/Identity/Kumbara.Identity.Application/"]
COPY ["src/Services/Identity/Kumbara.Identity.Infrastructure/Kumbara.Identity.Infrastructure.csproj", "src/Services/Identity/Kumbara.Identity.Infrastructure/"]
COPY ["src/Services/Identity/Kumbara.Identity.Api/Kumbara.Identity.Api.csproj", "src/Services/Identity/Kumbara.Identity.Api/"]

# Restore dependencies
RUN dotnet restore "src/Services/Identity/Kumbara.Identity.Api/Kumbara.Identity.Api.csproj"

# Copy source trees
COPY ["src/BuildingBlocks/", "src/BuildingBlocks/"]
COPY ["src/Services/Identity/", "src/Services/Identity/"]

# Publish in Release mode
WORKDIR "/src/src/Services/Identity/Kumbara.Identity.Api"
RUN dotnet publish "Kumbara.Identity.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Kumbara.Identity.Api.dll"]

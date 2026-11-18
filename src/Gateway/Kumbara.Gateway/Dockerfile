# =============================================================================
# Kumbara API Gateway - Dockerfile (.NET 8)
# Build Context: repository root
# =============================================================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy BuildingBlocks csproj files for restore caching
COPY ["src/BuildingBlocks/Kumbara.BuildingBlocks.Contracts/Kumbara.BuildingBlocks.Contracts.csproj", "src/BuildingBlocks/Kumbara.BuildingBlocks.Contracts/"]

# Copy Gateway project file
COPY ["src/Gateway/Kumbara.Gateway/Kumbara.Gateway.csproj", "src/Gateway/Kumbara.Gateway/"]

# Restore dependencies
RUN dotnet restore "src/Gateway/Kumbara.Gateway/Kumbara.Gateway.csproj"

# Copy source trees
COPY ["src/BuildingBlocks/", "src/BuildingBlocks/"]
COPY ["src/Gateway/Kumbara.Gateway/", "src/Gateway/Kumbara.Gateway/"]

# Publish in Release mode
WORKDIR "/src/src/Gateway/Kumbara.Gateway"
RUN dotnet publish "Kumbara.Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Kumbara.Gateway.dll"]

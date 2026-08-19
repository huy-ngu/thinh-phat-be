# Multi-stage build for minimal image size and ultra-low RAM footprint
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Cache dependencies layer
COPY ["ThinhPhat.Api.csproj", "./"]
RUN dotnet restore "ThinhPhat.Api.csproj"

# Build and Publish in Release mode with trimming-ready optimizations
COPY . .
RUN dotnet publish "ThinhPhat.Api.csproj" -c Release -o /app/publish \
    --no-restore \
    -p:UseAppHost=false

# Runtime image: lightweight ASP.NET 9 / 10
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# ==============================================================================
# CRITICAL MEMORY TUNING FOR RENDER FREE TIER (512MB RAM LIMIT):
# 1. Workstation GC (gcServer=0): Uses ~30MB baseline RAM vs ~150-250MB with Server GC.
# 2. Hard Heap Limit: Forces GC to trigger before Render OS cgroup kills process at 512MB.
# 3. Disable Diagnostics: Frees up internal diagnostic pipeline memory.
# ==============================================================================
ENV DOTNET_gcServer=0 \
    DOTNET_GCHeapHardLimitPercent=75 \
    DOTNET_EnableDiagnostics=0 \
    ASPNETCORE_URLS=http://+:10000 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 10000

ENTRYPOINT ["dotnet", "ThinhPhat.Api.dll"]

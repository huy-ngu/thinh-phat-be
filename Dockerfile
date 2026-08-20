# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

COPY ["ThinhPhat.Api.csproj", "./"]
RUN dotnet restore "ThinhPhat.Api.csproj"

COPY . .
RUN dotnet publish "ThinhPhat.Api.csproj" -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS final
WORKDIR /app
COPY --from=build /app/publish .

USER $APP_UID

ENV DOTNET_gcServer=0 \
    DOTNET_EnableDiagnostics=0 \
    ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "ThinhPhat.Api.dll"]
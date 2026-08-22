# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore com cache de camadas: copia só os arquivos de projeto primeiro
COPY Directory.Build.props Directory.Build.targets ./
COPY src/Api/Worfair.Api/Worfair.Api.csproj src/Api/Worfair.Api/
COPY src/BuildingBlocks/Worfair.BuildingBlocks.Domain/Worfair.BuildingBlocks.Domain.csproj src/BuildingBlocks/Worfair.BuildingBlocks.Domain/
COPY src/BuildingBlocks/Worfair.BuildingBlocks.Application/Worfair.BuildingBlocks.Application.csproj src/BuildingBlocks/Worfair.BuildingBlocks.Application/
COPY src/BuildingBlocks/Worfair.BuildingBlocks.Infrastructure/Worfair.BuildingBlocks.Infrastructure.csproj src/BuildingBlocks/Worfair.BuildingBlocks.Infrastructure/
COPY src/BuildingBlocks/Worfair.BuildingBlocks.Contracts/Worfair.BuildingBlocks.Contracts.csproj src/BuildingBlocks/Worfair.BuildingBlocks.Contracts/
COPY src/Modules/Identity/*/*.csproj src/Modules/Identity/
COPY src/Modules/Tenants/*/*.csproj src/Modules/Tenants/
COPY src/Modules/Jobs/*/*.csproj src/Modules/Jobs/
COPY src/Modules/Recruitment/*/*.csproj src/Modules/Recruitment/
COPY src/Modules/Proposals/*/*.csproj src/Modules/Proposals/
COPY src/Modules/Financial/*/*.csproj src/Modules/Financial/
COPY src/Modules/Notifications/*/*.csproj src/Modules/Notifications/

RUN dotnet restore src/Api/Worfair.Api/Worfair.Api.csproj

# Código-fonte completo
COPY . .
RUN dotnet publish src/Api/Worfair.Api/Worfair.Api.csproj -c Release -o /app/publish --no-restore

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Worfair.Api.dll"]
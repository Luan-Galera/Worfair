# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /

# Restore com cache de camadas: copia só os arquivos de projeto primeiro
COPY Directory.Build.props Directory.Build.targets ./
COPY src ./src

RUN dotnet restore /src/Api/Worfair.Api/Worfair.Api.csproj

# Código-fonte completo
COPY . .
RUN dotnet publish /src/Api/Worfair.Api/Worfair.Api.csproj -c Release -o /app/publish --no-restore

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Worfair.Api.dll"]
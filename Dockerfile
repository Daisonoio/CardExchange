# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copia i file di progetto per il restore (cache layer)
COPY CardExchange.Core/CardExchange.Core.csproj CardExchange.Core/
COPY CardExchange.Infrastructure/CardExchange.Infrastructure.csproj CardExchange.Infrastructure/
COPY CardExchange.API/CardExchange.API.csproj CardExchange.API/
RUN dotnet restore CardExchange.API/CardExchange.API.csproj

# Copia tutto il codice sorgente
COPY . .

# Build e publish
RUN dotnet publish CardExchange.API/CardExchange.API.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Utente non-root per sicurezza
RUN groupadd -r appuser && useradd -r -g appuser appuser

COPY --from=build /app/publish .

# Health check (usa wget, disponibile nel runtime image)
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

# Esponi porta
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

USER appuser
ENTRYPOINT ["dotnet", "CardExchange.API.dll"]

# Fase 1: restauracion
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Erp.Server.sln ./
COPY src/Erp.Server/Erp.Server.csproj src/Erp.Server/
RUN dotnet restore src/Erp.Server/Erp.Server.csproj

COPY src/Erp.Server/ src/Erp.Server/
RUN dotnet publish src/Erp.Server/Erp.Server.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# Fase 2: imagen final minima
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

# La imagen base ya incluye un usuario de sistema 'app' sin privilegios.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./

USER app

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl --fail --silent --show-error http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "Erp.Server.dll"]
# Multi-stage: SDK só no build; runtime final enxuto e não-root.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/Platform/ src/Platform/
COPY src/Telemetry.Ingest/ src/Telemetry.Ingest/
RUN dotnet publish src/Telemetry.Ingest/Telemetry.Ingest.Worker -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Telemetry.Ingest.Worker.dll"]

# Contexto de build = raiz do repo (o CI chama: docker build -f src/Edge.ProtocolGateway/Dockerfile .)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/Platform/ src/Platform/
COPY src/Edge.ProtocolGateway/ src/Edge.ProtocolGateway/
RUN dotnet publish src/Edge.ProtocolGateway/Edge.ProtocolGateway.Worker -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Edge.ProtocolGateway.Worker.dll"]

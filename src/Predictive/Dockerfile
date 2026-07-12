# Contexto de build = raiz do repo (o CI chama: docker build -f src/Predictive/Dockerfile .)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/Platform/ src/Platform/
COPY src/Predictive/ src/Predictive/
RUN dotnet publish src/Predictive/Predictive.Worker -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Predictive.Worker.dll"]

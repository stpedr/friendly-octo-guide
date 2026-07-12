# Contexto de build = raiz do repo (o CI chama: docker build -f src/Decision.Engine/Dockerfile .)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/Platform/ src/Platform/
COPY src/Decision.Engine/ src/Decision.Engine/
RUN dotnet publish src/Decision.Engine/Decision.Engine.Worker -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Decision.Engine.Worker.dll"]

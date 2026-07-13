# Contexto de build = raiz do repo (o CI chama: docker build -f src/Knowledge/Dockerfile .)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/Platform/ src/Platform/
COPY src/Knowledge/ src/Knowledge/
RUN dotnet publish src/Knowledge/Knowledge.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Knowledge.Api.dll"]

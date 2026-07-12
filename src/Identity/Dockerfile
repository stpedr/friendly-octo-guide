# Contexto de build = raiz do repo (o CI chama: docker build -f src/Identity/Dockerfile .)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/Platform/ src/Platform/
COPY src/Identity/ src/Identity/
RUN dotnet publish src/Identity/Identity.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Identity.Api.dll"]

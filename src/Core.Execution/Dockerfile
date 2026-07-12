# Contexto de build = raiz do repo (o CI chama: docker build -f src/Core.Execution/Dockerfile .)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/Platform/ src/Platform/
COPY src/Core.Execution/ src/Core.Execution/
RUN dotnet publish src/Core.Execution/Core.Execution.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Core.Execution.Api.dll"]

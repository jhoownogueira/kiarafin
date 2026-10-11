FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/KiaraFin.Api/KiaraFin.Api.csproj src/KiaraFin.Api/
RUN dotnet restore src/KiaraFin.Api/KiaraFin.Api.csproj

COPY src/KiaraFin.Api/ src/KiaraFin.Api/
RUN dotnet publish src/KiaraFin.Api/KiaraFin.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "KiaraFin.Api.dll"]

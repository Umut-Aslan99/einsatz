# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props .editorconfig ./
COPY src/Einsatz.Domain/Einsatz.Domain.csproj src/Einsatz.Domain/
COPY src/Einsatz.Api/Einsatz.Api.csproj src/Einsatz.Api/
RUN dotnet restore src/Einsatz.Api/Einsatz.Api.csproj
COPY src/ src/
RUN dotnet publish src/Einsatz.Api/Einsatz.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# fontconfig for PDF rendering, tzdata so "today" means Europe/Berlin, curl for the healthcheck.
RUN apt-get update && apt-get install -y --no-install-recommends libfontconfig1 tzdata curl \
 && rm -rf /var/lib/apt/lists/* \
 && mkdir -p /data && chown app:app /data
ENV TZ=Europe/Berlin ASPNETCORE_HTTP_PORTS=8080 DOTNET_gcServer=0
WORKDIR /app
COPY --from=build /out .
USER app
VOLUME /data
HEALTHCHECK --interval=30s --timeout=3s --start-period=30s --start-interval=2s CMD curl -fs http://127.0.0.1:8080/healthz || exit 1
ENTRYPOINT ["dotnet", "Einsatz.Api.dll"]

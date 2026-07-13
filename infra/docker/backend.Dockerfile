ARG DOTNET_VERSION=10.0
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG WMS_PROJECT=Wms.Api
WORKDIR /source
COPY global.json Directory.Build.props ./
COPY src/backend ./src/backend
RUN dotnet restore "src/backend/${WMS_PROJECT}/${WMS_PROJECT}.csproj"
RUN dotnet publish "src/backend/${WMS_PROJECT}/${WMS_PROJECT}.csproj" --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
ARG WMS_PROJECT=Wms.Api
ENV WMS_PROJECT=${WMS_PROJECT}
RUN apt-get update \
    && apt-get install --yes --no-install-recommends ca-certificates curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish ./
USER app
ENTRYPOINT ["sh", "-c", "exec dotnet \"${WMS_PROJECT}.dll\""]

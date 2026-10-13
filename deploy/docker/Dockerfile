# syntax=docker/dockerfile:1.7
# Builds the NotifyHub API and background worker images from one build stage.
#   docker build -f deploy/docker/Dockerfile --target api    -t notifyhub-api .
#   docker build -f deploy/docker/Dockerfile --target worker -t notifyhub-worker .
# Production deployments should pin the base images below by digest.

ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0

FROM ${DOTNET_SDK_IMAGE} AS build
WORKDIR /src

# Optional extra CA certificate for builds behind a TLS-intercepting proxy:
#   docker build --secret id=extra_ca,src=/path/to/ca.crt ...
RUN --mount=type=secret,id=extra_ca,required=false \
    if [ -f /run/secrets/extra_ca ]; then \
      cp /run/secrets/extra_ca /usr/local/share/ca-certificates/extra-ca.crt && update-ca-certificates; \
    fi

COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/NotifyHub.Domain/NotifyHub.Domain.csproj src/NotifyHub.Domain/
COPY src/NotifyHub.Contracts/NotifyHub.Contracts.csproj src/NotifyHub.Contracts/
COPY src/NotifyHub.Application/NotifyHub.Application.csproj src/NotifyHub.Application/
COPY src/NotifyHub.Infrastructure/NotifyHub.Infrastructure.csproj src/NotifyHub.Infrastructure/
COPY src/NotifyHub.Api/NotifyHub.Api.csproj src/NotifyHub.Api/
COPY src/NotifyHub.Worker/NotifyHub.Worker.csproj src/NotifyHub.Worker/
RUN dotnet restore src/NotifyHub.Api/NotifyHub.Api.csproj \
 && dotnet restore src/NotifyHub.Worker/NotifyHub.Worker.csproj

COPY src/ src/
ARG CI=true
RUN dotnet publish src/NotifyHub.Api/NotifyHub.Api.csproj -c Release -o /out/api --no-restore -p:UseAppHost=false \
 && dotnet publish src/NotifyHub.Worker/NotifyHub.Worker.csproj -c Release -o /out/worker --no-restore -p:UseAppHost=false

FROM ${DOTNET_RUNTIME_IMAGE} AS api
WORKDIR /app
COPY --from=build /out/api .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=0
EXPOSE 8080
# Non-root user provided by the .NET base images.
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=10s --start-period=20s --retries=3 CMD ["dotnet", "NotifyHub.Api.dll", "healthcheck"]
ENTRYPOINT ["dotnet", "NotifyHub.Api.dll"]

FROM ${DOTNET_RUNTIME_IMAGE} AS worker
WORKDIR /app
COPY --from=build /out/worker .
ENV Worker__Heartbeat__LivenessFilePath=/tmp/notifyhub-worker-heartbeat
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 CMD ["dotnet", "NotifyHub.Worker.dll", "healthcheck"]
ENTRYPOINT ["dotnet", "NotifyHub.Worker.dll"]

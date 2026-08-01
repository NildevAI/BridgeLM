# NilDev.BridgeLM

NilDev.BridgeLM is a .NET 10 bridge service that proxies LLM traffic, records requests and responses with SQLite, broadcasts live lifecycle events through SignalR, and serves a plain JavaScript monitoring dashboard from the same container image.

## Current shape

- `src/NilDev.BridgeLM` hosts the ASP.NET Core proxy, SignalR hub, health endpoint, configuration API, and static dashboard assets.
- `src/NilDev.BridgeLM.Application` contains orchestration for request tracking, forwarding, configuration updates, and event emission.
- `src/NilDev.BridgeLM.Domain` contains contracts and models for proxying, persistence, runtime configuration, and live monitoring.
- `src/NilDev.BridgeLM.Infrastructure` contains SQLite persistence, schema initialization, and the upstream HTTP forwarder.
- `tests` contains unit and integration tests.

## Endpoints

- `GET /api/config` returns the effective runtime configuration with API key presence masked.
- `PUT /api/config` updates runtime configuration in memory.
- `GET /api/requests` returns recent proxied requests.
- `GET /api/requests/{id}` returns captured request and response details.
- `ALL /proxy/{**path}` forwards traffic to the configured upstream LLM endpoint.
- `GET /health` exposes a basic health check.
- `/hubs/bridge` exposes the SignalR hub for live request lifecycle events.

## Build and test

### Backend

```powershell
dotnet build .\src\NilDev.BridgeLM\NilDev.BridgeLM.csproj -c Release
dotnet publish .\src\NilDev.BridgeLM\NilDev.BridgeLM.csproj -c Release -r win-x64 /p:PublishAot=true
dotnet test .\tests\NilDev.BridgeLM.Application.Tests\NilDev.BridgeLM.Application.Tests.csproj -c Release
```

The dashboard lives directly under `src/NilDev.BridgeLM/wwwroot` and is served as static assets by the ASP.NET Core host. No separate Node, npm, React, or Vite build is required.

## Container build

```powershell
docker build -t nildev-bridgelm:latest .
docker compose up --build
```

The Dockerfile publishes the backend with Native AOT for `linux-x64`, including the dashboard static assets already stored under `wwwroot`, and produces a single runnable image.

Podman can use the same files:

```powershell
podman build -t nildev-bridgelm:latest .
podman compose up --build
```

## Configuration

Runtime settings come from `appsettings.json` and environment variables. Important keys:

- `Bridge__Backend__Name`
- `Bridge__Backend__BaseUrl`
- `Bridge__Backend__ApiKeyHeader`
- `Bridge__Backend__ApiKey`
- `Bridge__Storage__ConnectionString`
- `Bridge__Storage__RecentRequestLimit`

## Notes

- Local Windows-to-Linux Native AOT cross-compilation is not supported directly by the installed SDK. Use the Docker build to validate the `linux-x64` AOT artifact.
- The container runtime path uses direct `Microsoft.Data.Sqlite` access because plain Dapper triggers runtime code generation under Native AOT.
- The current runtime configuration update endpoint is in-memory. Persisted configuration can be added later without changing the public API shape.
- Transparent Copilot compatibility still needs characterization against real client traffic before production use.

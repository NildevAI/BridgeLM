FROM node:24-bookworm-slim AS dashboard-build
WORKDIR /workspace/src/NilDev.BridgeLM.Dashboard
COPY src/NilDev.BridgeLM.Dashboard/package.json ./
COPY src/NilDev.BridgeLM.Dashboard/tsconfig.json ./
COPY src/NilDev.BridgeLM.Dashboard/tsconfig.app.json ./
COPY src/NilDev.BridgeLM.Dashboard/tsconfig.node.json ./
COPY src/NilDev.BridgeLM.Dashboard/vite.config.ts ./
COPY src/NilDev.BridgeLM.Dashboard/index.html ./
COPY src/NilDev.BridgeLM.Dashboard/src ./src
RUN npm install --no-fund --no-audit
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
WORKDIR /workspace
RUN apt-get update \
	&& apt-get install -y --no-install-recommends clang zlib1g-dev \
	&& rm -rf /var/lib/apt/lists/*
COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY src ./src
COPY --from=dashboard-build /workspace/src/NilDev.BridgeLM/wwwroot ./src/NilDev.BridgeLM/wwwroot
RUN dotnet publish ./src/NilDev.BridgeLM/NilDev.BridgeLM.csproj -c Release -r linux-x64 /p:PublishAot=true -o /out

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime
WORKDIR /app
COPY --from=publish /out ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
VOLUME ["/app/data"]
ENTRYPOINT ["./NilDev.BridgeLM"]

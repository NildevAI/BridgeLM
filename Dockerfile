FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
WORKDIR /workspace
RUN apt-get update \
	&& apt-get install -y --no-install-recommends clang \
	&& rm -rf /var/lib/apt/lists/*
COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY src ./src
RUN dotnet publish ./src/NilDev.BridgeLM/NilDev.BridgeLM.csproj -c Release -r linux-x64 /p:PublishAot=true -o /out

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime
WORKDIR /app
COPY --from=publish /out ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
VOLUME ["/app/data"]
ENTRYPOINT ["./NilDev.BridgeLM"]

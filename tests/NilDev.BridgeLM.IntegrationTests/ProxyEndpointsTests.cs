namespace NilDev.BridgeLM.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NilDev.BridgeLM.Domain.Abstractions;
using NilDev.BridgeLM.Domain.Models;
using NilDev.BridgeLM.Serialization;
using NilDev.BridgeLM.IntegrationTests.Serialization;
using Xunit;

public sealed class ProxyEndpointsTests : IClassFixture<ProxyWebApplicationFactory>
{
    private readonly HttpClient client;
    private readonly ProxyWebApplicationFactory factory;

    public ProxyEndpointsTests(ProxyWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public void SignalRJsonProtocol_UsesBridgeSerializerContext()
    {
        var options = factory.Services.GetRequiredService<IOptions<JsonHubProtocolOptions>>().Value;

        options.PayloadSerializerOptions.TypeInfoResolverChain.Should().NotBeEmpty();
        options.PayloadSerializerOptions.GetTypeInfo(typeof(ProxyResponseChunk)).Should().NotBeNull();
    }

    [Fact]
    public void SignalRJsonProtocol_CanSerializeProxyResponseChunk()
    {
        var serializerOptions = factory.Services
            .GetRequiredService<IOptions<JsonHubProtocolOptions>>()
            .Value
            .PayloadSerializerOptions;

        JsonSerializer.Serialize(
            new ProxyResponseChunk
            {
                RequestId = Guid.NewGuid().ToString("N"),
                Content = "chunk",
                TimestampUtc = DateTimeOffset.UtcNow
            },
            serializerOptions).Should().Contain("chunk");
    }

    [Fact]
    public async Task GetConfig_ReturnsConfiguredSnapshot()
    {
        var response = await client.GetAsync("/api/config");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync(
            IntegrationTestJsonContext.Default.BridgeConfigurationView);
        payload.Should().NotBeNull();
        payload!.BackendName.Should().Be("IntegrationBackend");
        payload.HasApiKey.Should().BeTrue();
    }

    [Fact]
    public async Task ConfigCatalog_SupportsCreateSelectDuplicateRenameAndDelete()
    {
        var listResponse = await client.GetAsync("/api/configs");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialList = await listResponse.Content.ReadFromJsonAsync(
            IntegrationTestJsonContext.Default.ListBridgeNamedConfigurationSummary);
        initialList.Should().NotBeNull();
        initialList!.Should().ContainSingle(configuration => configuration.Name == "Primary");
        initialList[0].IsActive.Should().BeTrue();

        var createResponse = await client.PostAsJsonAsync(
            "/api/configs",
            new BridgeNamedConfigurationCreate
            {
                Name = "Secondary",
                BackendName = "SecondaryBackend",
                BackendBaseUrl = "http://secondary.local/",
                RecentRequestLimit = 25
            },
            IntegrationTestJsonContext.Default.BridgeNamedConfigurationCreate);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync(
            IntegrationTestJsonContext.Default.BridgeNamedConfigurationView);
        created.Should().NotBeNull();
        created!.Configuration.BackendName.Should().Be("SecondaryBackend");

        var selectResponse = await client.PostAsync("/api/configs/Secondary/select", content: null);
        selectResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var activeConfigResponse = await client.GetAsync("/api/config");
        var activeConfig = await activeConfigResponse.Content.ReadFromJsonAsync(
            IntegrationTestJsonContext.Default.BridgeConfigurationView);
        activeConfig.Should().NotBeNull();
        activeConfig!.BackendName.Should().Be("SecondaryBackend");

        var duplicateResponse = await client.PostAsJsonAsync(
            "/api/configs/Secondary/duplicate",
            new BridgeDuplicateConfigurationRequest { Name = "Secondary Copy" },
            IntegrationTestJsonContext.Default.BridgeDuplicateConfigurationRequest);
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var renameResponse = await client.PostAsJsonAsync(
            "/api/configs/Secondary/rename",
            new BridgeRenameConfigurationRequest { Name = "Renamed" },
            IntegrationTestJsonContext.Default.BridgeRenameConfigurationRequest);
        renameResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var renamed = await renameResponse.Content.ReadFromJsonAsync(
            IntegrationTestJsonContext.Default.BridgeNamedConfigurationView);
        renamed.Should().NotBeNull();
        renamed!.Name.Should().Be("Renamed");
        renamed.IsActive.Should().BeTrue();

        var resetActiveResponse = await client.PostAsync("/api/configs/Primary/select", content: null);
        resetActiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteResponse = await client.DeleteAsync("/api/configs/Renamed");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var duplicateDeleteResponse = await client.DeleteAsync("/api/configs/Secondary%20Copy");
        duplicateDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var finalListResponse = await client.GetAsync("/api/configs");
        var finalList = await finalListResponse.Content.ReadFromJsonAsync(
            IntegrationTestJsonContext.Default.ListBridgeNamedConfigurationSummary);
        finalList.Should().NotBeNull();
        finalList!.Should().ContainSingle(configuration => configuration.Name == "Primary" && configuration.IsActive);
    }

    [Fact]
    public async Task ProxyRequest_IsForwardedAndLogged()
    {
        var response = await client.PostAsync(
            "/proxy/v1/chat/completions?model=gpt-test",
            new StringContent("{\"prompt\":\"ping\"}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("proxied");

        var requestsResponse = await client.GetAsync("/api/requests");
        var requests = await requestsResponse.Content.ReadFromJsonAsync(
            IntegrationTestJsonContext.Default.ListProxyRequestSummary);
        requests.Should().NotBeNull();
        requests!.Should().ContainSingle();
        requests[0].Status.Should().Be("Completed");
        requests[0].Path.Should().Be("/v1/chat/completions");

        var detailsResponse = await client.GetAsync($"/api/requests/{requests[0].Id}");
        var details = await detailsResponse.Content.ReadFromJsonAsync(
            IntegrationTestJsonContext.Default.ProxyRequestLog);
        details.Should().NotBeNull();
        details!.ResponseBody.Should().Contain("proxied");
    }

    [Fact]
    public async Task RequestLogEndpoints_SupportDeleteAndTruncate()
    {
        await client.PostAsync(
            "/proxy/v1/chat/completions?model=gpt-delete",
            new StringContent("{\"prompt\":\"delete-one\"}", Encoding.UTF8, "application/json"));
        await client.PostAsync(
            "/proxy/v1/chat/completions?model=gpt-delete",
            new StringContent("{\"prompt\":\"delete-all\"}", Encoding.UTF8, "application/json"));

        var requestsResponse = await client.GetAsync("/api/requests");
        var requests = await requestsResponse.Content.ReadFromJsonAsync(
            IntegrationTestJsonContext.Default.ListProxyRequestSummary);

        requests.Should().NotBeNull();
        requests!.Should().HaveCount(2);

        var deleteResponse = await client.DeleteAsync($"/api/requests/{requests[0].Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDeleteResponse = await client.GetAsync("/api/requests");
        var afterDelete = await afterDeleteResponse.Content.ReadFromJsonAsync(
            IntegrationTestJsonContext.Default.ListProxyRequestSummary);

        afterDelete.Should().NotBeNull();
        afterDelete!.Should().HaveCount(1);
        afterDelete[0].Id.Should().NotBe(requests[0].Id);

        var truncateResponse = await client.DeleteAsync("/api/requests");
        truncateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var emptyResponse = await client.GetAsync("/api/requests");
        var empty = await emptyResponse.Content.ReadFromJsonAsync(
            IntegrationTestJsonContext.Default.ListProxyRequestSummary);

        empty.Should().NotBeNull();
        empty.Should().BeEmpty();
    }
}

public sealed class ProxyWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            var runtimeStore = new IntegrationSettingsStore();
            ReplaceService<IBridgeRuntimeSettingsStore>(services, runtimeStore);
            ReplaceService<IBridgeConfigurationStore>(services, new IntegrationConfigurationStore(runtimeStore));
            ReplaceService<ILlmForwarder>(services, new IntegrationForwarder());
            ReplaceService<IProxyEventSink>(services, new NullProxyEventSink());
        });
    }

    private static void ReplaceService<TService>(IServiceCollection services, TService implementation)
        where TService : class
    {
        var existingDescriptors = services.Where(descriptor => descriptor.ServiceType == typeof(TService)).ToList();
        foreach (var descriptor in existingDescriptors)
        {
            services.Remove(descriptor);
        }

        services.AddSingleton(implementation);
    }

    private sealed class IntegrationSettingsStore : IBridgeRuntimeSettingsStore
    {
        private BridgeRuntimeOptions options = CreateOptions();

        public BridgeRuntimeOptions GetCurrent() => Clone(options);

        public void Update(BridgeRuntimeOptions next)
        {
            options = Clone(next);
        }

        public static BridgeRuntimeOptions CreateOptions() => new()
        {
            Backend = new BridgeBackendOptions
            {
                Name = "IntegrationBackend",
                BaseUrl = "http://localhost:9999/",
                ApiKeyHeader = "Authorization",
                ApiKey = "integration-key"
            },
            Storage = new BridgeStorageOptions
            {
                ConnectionString = $"Data Source={Path.Combine(Path.GetTempPath(), $"bridgelm-{Guid.NewGuid():N}.db")}",
                RecentRequestLimit = 10
            }
        };
    }

    private sealed class IntegrationConfigurationStore(IntegrationSettingsStore runtimeStore) : IBridgeConfigurationStore
    {
        private readonly Dictionary<string, BridgeSavedConfiguration> configurations = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Primary"] = new BridgeSavedConfiguration
            {
                Name = "Primary",
                Options = Clone(runtimeStore.GetCurrent()),
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            }
        };

        public Task EnsureInitializedAsync(BridgeSavedConfiguration seedConfiguration, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<BridgeSavedConfiguration>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BridgeSavedConfiguration>>(
                configurations.Values
                    .OrderBy(configuration => configuration.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(CloneSavedConfiguration)
                    .ToList());

        public Task<BridgeSavedConfiguration?> GetAsync(string name, CancellationToken cancellationToken)
        {
            configurations.TryGetValue(name, out var configuration);
            return Task.FromResult(configuration is null ? null : CloneSavedConfiguration(configuration));
        }

        public Task CreateOrUpdateAsync(BridgeSavedConfiguration configuration, CancellationToken cancellationToken)
        {
            configurations[configuration.Name] = CloneSavedConfiguration(configuration);
            return Task.CompletedTask;
        }

        public Task<bool> RenameAsync(string currentName, string newName, CancellationToken cancellationToken)
        {
            if (!configurations.Remove(currentName, out var configuration))
            {
                return Task.FromResult(false);
            }

            configurations[newName] = new BridgeSavedConfiguration
            {
                Name = newName,
                Options = Clone(configuration.Options),
                IsActive = configuration.IsActive,
                CreatedAtUtc = configuration.CreatedAtUtc,
                UpdatedAtUtc = configuration.UpdatedAtUtc
            };

            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(configurations.Remove(name));

        public Task<bool> SetActiveAsync(string name, CancellationToken cancellationToken)
        {
            if (!configurations.ContainsKey(name))
            {
                return Task.FromResult(false);
            }

            foreach (var key in configurations.Keys.ToList())
            {
                var configuration = configurations[key];
                configurations[key] = new BridgeSavedConfiguration
                {
                    Name = configuration.Name,
                    Options = Clone(configuration.Options),
                    IsActive = string.Equals(key, name, StringComparison.OrdinalIgnoreCase),
                    CreatedAtUtc = configuration.CreatedAtUtc,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                };
            }

            return Task.FromResult(true);
        }
    }

    private static BridgeSavedConfiguration CloneSavedConfiguration(BridgeSavedConfiguration configuration) => new()
    {
        Name = configuration.Name,
        Options = Clone(configuration.Options),
        IsActive = configuration.IsActive,
        CreatedAtUtc = configuration.CreatedAtUtc,
        UpdatedAtUtc = configuration.UpdatedAtUtc
    };

    private sealed class IntegrationForwarder : ILlmForwarder
    {
        public Task<HttpResponseMessage> SendAsync(BridgeBackendOptions backend, ProxyInboundRequest request, CancellationToken cancellationToken)
        {
            var message = new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent($"{{\"status\":\"proxied\",\"path\":\"{request.Path}\"}}", Encoding.UTF8, "application/json")
            };

            return Task.FromResult(message);
        }
    }

    private sealed class NullProxyEventSink : IProxyEventSink
    {
        public Task RequestStartedAsync(ProxyRequestSummary summary, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ResponseChunkAsync(ProxyResponseChunk chunk, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RequestCompletedAsync(ProxyRequestSummary summary, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static BridgeRuntimeOptions Clone(BridgeRuntimeOptions options) => new()
    {
        Backend = new BridgeBackendOptions
        {
            Name = options.Backend.Name,
            BaseUrl = options.Backend.BaseUrl,
            ApiKeyHeader = options.Backend.ApiKeyHeader,
            ApiKey = options.Backend.ApiKey,
            DefaultHeaders = new Dictionary<string, string>(options.Backend.DefaultHeaders, StringComparer.OrdinalIgnoreCase)
        },
        Storage = new BridgeStorageOptions
        {
            ConnectionString = options.Storage.ConnectionString,
            RecentRequestLimit = options.Storage.RecentRequestLimit
        }
    };
}

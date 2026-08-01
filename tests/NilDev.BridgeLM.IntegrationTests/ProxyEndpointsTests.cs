namespace NilDev.BridgeLM.IntegrationTests;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NilDev.BridgeLM.Domain.Abstractions;
using NilDev.BridgeLM.Domain.Models;
using NilDev.BridgeLM.IntegrationTests.Serialization;
using Xunit;

public sealed class ProxyEndpointsTests : IClassFixture<ProxyWebApplicationFactory>
{
    private readonly HttpClient client;

    public ProxyEndpointsTests(ProxyWebApplicationFactory factory)
    {
        client = factory.CreateClient();
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
}

public sealed class ProxyWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            ReplaceService<IBridgeRuntimeSettingsStore>(services, new IntegrationSettingsStore());
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
        private readonly BridgeRuntimeOptions options = new()
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

        public BridgeRuntimeOptions GetCurrent() => options;

        public void Update(BridgeRuntimeOptions options)
        {
        }
    }

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
}

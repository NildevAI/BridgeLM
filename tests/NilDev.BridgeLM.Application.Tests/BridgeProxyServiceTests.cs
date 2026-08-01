namespace NilDev.BridgeLM.Application.Tests;

using System.Net;
using System.Text;
using FluentAssertions;
using NilDev.BridgeLM.Application.Services;
using NilDev.BridgeLM.Domain.Abstractions;
using NilDev.BridgeLM.Domain.Models;
using Xunit;

public sealed class BridgeProxyServiceTests
{
    [Fact]
    public void UpdateConfiguration_PreservesSecret_WhenApiKeyIsNotProvided()
    {
        var settingsStore = new TestSettingsStore();
        var service = CreateService(settingsStore);

        var updated = service.UpdateConfiguration(new BridgeConfigurationUpdate
        {
            BackendBaseUrl = "http://localhost:8080/",
            RecentRequestLimit = 25
        });

        updated.BackendBaseUrl.Should().Be("http://localhost:8080/");
        updated.RecentRequestLimit.Should().Be(25);
        updated.HasApiKey.Should().BeTrue();
        settingsStore.GetCurrent().Backend.ApiKey.Should().Be("secret-value");
    }

    [Fact]
    public async Task StartAndCompleteAsync_StoresRequestAndPublishesEvents()
    {
        var settingsStore = new TestSettingsStore();
        var logStore = new InMemoryRequestLogStore();
        var eventSink = new RecordingProxyEventSink();
        var service = CreateService(settingsStore, logStore, eventSink);

        var session = await service.StartProxyAsync(
            new ProxyInboundRequest
            {
                Method = "POST",
                Path = "/v1/chat/completions",
                QueryString = "",
                ContentType = "application/json",
                Body = Encoding.UTF8.GetBytes("{\"prompt\":\"ping\"}"),
                Headers = new Dictionary<string, string[]> { ["X-Test"] = ["value"] }
            },
            CancellationToken.None);

        await service.PublishChunkAsync(session.RequestId, Encoding.UTF8.GetBytes("pong"), CancellationToken.None);
        await service.CompleteAsync(session, "{}", "pong", CancellationToken.None);
        var persisted = await service.GetAsync(session.RequestId, CancellationToken.None);

        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be("Completed");
        persisted.ResponseBody.Should().Be("pong");
        eventSink.Started.Should().ContainSingle();
        eventSink.Chunks.Should().ContainSingle(chunk => chunk.Content == "pong");
        eventSink.Completed.Should().ContainSingle(summary => summary.Id == session.RequestId && summary.Status == "Completed");

        await session.DisposeAsync();
    }

    private static BridgeProxyService CreateService(
        TestSettingsStore? settingsStore = null,
        InMemoryRequestLogStore? logStore = null,
        RecordingProxyEventSink? eventSink = null)
    {
        settingsStore ??= new TestSettingsStore();
        logStore ??= new InMemoryRequestLogStore();
        eventSink ??= new RecordingProxyEventSink();

        return new BridgeProxyService(
            settingsStore,
            new StubForwarder(),
            logStore,
            eventSink,
            [new NoOpRequestTransform()],
            [new NoOpResponseTransform()],
            TimeProvider.System);
    }

    private sealed class StubForwarder : ILlmForwarder
    {
        public Task<HttpResponseMessage> SendAsync(BridgeBackendOptions backend, ProxyInboundRequest request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("pong", Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }

    private sealed class TestSettingsStore : IBridgeRuntimeSettingsStore
    {
        private BridgeRuntimeOptions current = new()
        {
            Backend = new BridgeBackendOptions
            {
                Name = "TestBackend",
                BaseUrl = "http://localhost:11434/",
                ApiKeyHeader = "Authorization",
                ApiKey = "secret-value"
            },
            Storage = new BridgeStorageOptions
            {
                ConnectionString = "Data Source=:memory:",
                RecentRequestLimit = 10
            }
        };

        public BridgeRuntimeOptions GetCurrent() => current;

        public void Update(BridgeRuntimeOptions options)
        {
            current = options;
        }
    }

    private sealed class InMemoryRequestLogStore : IRequestLogStore
    {
        private readonly Dictionary<string, ProxyRequestLog> logs = new(StringComparer.Ordinal);

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AddAsync(ProxyRequestLog log, CancellationToken cancellationToken)
        {
            logs[log.Id] = log;
            return Task.CompletedTask;
        }

        public Task CompleteAsync(string requestId, int statusCode, string responseHeaders, string responseBody, long durationMs, CancellationToken cancellationToken)
        {
            var current = logs[requestId];
            logs[requestId] = WithCompletion(current, "Completed", statusCode, responseHeaders, responseBody, durationMs, null);
            return Task.CompletedTask;
        }

        public Task FailAsync(string requestId, string error, long durationMs, CancellationToken cancellationToken)
        {
            var current = logs[requestId];
            logs[requestId] = WithCompletion(current, "Failed", null, null, null, durationMs, error);
            return Task.CompletedTask;
        }

        public Task<ProxyRequestLog?> GetAsync(string requestId, CancellationToken cancellationToken)
        {
            logs.TryGetValue(requestId, out var log);
            return Task.FromResult(log);
        }

        public Task<IReadOnlyList<ProxyRequestSummary>> ListRecentAsync(int limit, CancellationToken cancellationToken)
        {
            var summaries = logs.Values.Select(log => new ProxyRequestSummary
            {
                Id = log.Id,
                Method = log.Method,
                Path = log.Path,
                StartedAtUtc = log.StartedAtUtc,
                Status = log.Status,
                BackendName = log.BackendName,
                ResponseStatusCode = log.ResponseStatusCode,
                DurationMs = log.DurationMs
            }).ToList();

            return Task.FromResult<IReadOnlyList<ProxyRequestSummary>>(summaries);
        }

        private static ProxyRequestLog WithCompletion(
            ProxyRequestLog current,
            string status,
            int? statusCode,
            string? responseHeaders,
            string? responseBody,
            long durationMs,
            string? error) => new()
        {
            Id = current.Id,
            Method = current.Method,
            Path = current.Path,
            QueryString = current.QueryString,
            RequestHeaders = current.RequestHeaders,
            RequestBody = current.RequestBody,
            BackendName = current.BackendName,
            BackendUrl = current.BackendUrl,
            StartedAtUtc = current.StartedAtUtc,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            Status = status,
            ResponseStatusCode = statusCode,
            ResponseHeaders = responseHeaders,
            ResponseBody = responseBody,
            DurationMs = durationMs,
            Error = error
        };
    }

    private sealed class RecordingProxyEventSink : IProxyEventSink
    {
        public List<ProxyRequestSummary> Started { get; } = [];

        public List<ProxyResponseChunk> Chunks { get; } = [];

        public List<ProxyRequestSummary> Completed { get; } = [];

        public Task RequestStartedAsync(ProxyRequestSummary summary, CancellationToken cancellationToken)
        {
            Started.Add(summary);
            return Task.CompletedTask;
        }

        public Task ResponseChunkAsync(ProxyResponseChunk chunk, CancellationToken cancellationToken)
        {
            Chunks.Add(chunk);
            return Task.CompletedTask;
        }

        public Task RequestCompletedAsync(ProxyRequestSummary summary, CancellationToken cancellationToken)
        {
            Completed.Add(summary);
            return Task.CompletedTask;
        }
    }
}

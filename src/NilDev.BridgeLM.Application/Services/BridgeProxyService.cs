namespace NilDev.BridgeLM.Application.Services;

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NilDev.BridgeLM.Application.Serialization;
using NilDev.BridgeLM.Domain.Abstractions;
using NilDev.BridgeLM.Domain.Models;

public sealed class BridgeProxyService(
    IBridgeRuntimeSettingsStore runtimeSettingsStore,
    ILlmForwarder llmForwarder,
    IRequestLogStore requestLogStore,
    IProxyEventSink proxyEventSink,
    IEnumerable<IRequestTransform> requestTransforms,
    IEnumerable<IResponseTransform> responseTransforms,
    TimeProvider timeProvider)
{
    public async Task<ProxyForwardSession> StartProxyAsync(
        ProxyInboundRequest inboundRequest,
        CancellationToken cancellationToken)
    {
        var transformedRequest = inboundRequest;
        foreach (var transform in requestTransforms)
        {
            transformedRequest = await transform.TransformAsync(transformedRequest, cancellationToken);
        }

        var currentOptions = runtimeSettingsStore.GetCurrent();
        var startedAtUtc = timeProvider.GetUtcNow();
        var requestId = Guid.CreateVersion7().ToString();

        var requestLog = new ProxyRequestLog
        {
            Id = requestId,
            Method = transformedRequest.Method,
            Path = transformedRequest.Path,
            QueryString = transformedRequest.QueryString,
            RequestHeaders = JsonSerializer.Serialize(
                transformedRequest.Headers,
                ApplicationJsonSerializerContext.Default.DictionaryStringStringArray),
            RequestBody = Encoding.UTF8.GetString(transformedRequest.Body),
            BackendName = currentOptions.Backend.Name,
            BackendUrl = currentOptions.Backend.BaseUrl,
            StartedAtUtc = startedAtUtc,
            Status = "Pending"
        };

        await requestLogStore.AddAsync(requestLog, cancellationToken);
        await proxyEventSink.RequestStartedAsync(ToSummary(requestLog), cancellationToken);

        var upstreamResponse = await llmForwarder.SendAsync(currentOptions.Backend, transformedRequest, cancellationToken);

        return new ProxyForwardSession
        {
            RequestId = requestId,
            StartedAtUtc = startedAtUtc,
            StartedTimestamp = Stopwatch.GetTimestamp(),
            Method = transformedRequest.Method,
            Path = transformedRequest.Path,
            BackendName = currentOptions.Backend.Name,
            BackendUrl = currentOptions.Backend.BaseUrl,
            UpstreamResponse = upstreamResponse
        };
    }

    public Task<IReadOnlyList<ProxyRequestSummary>> ListRecentAsync(CancellationToken cancellationToken)
    {
        var limit = runtimeSettingsStore.GetCurrent().Storage.RecentRequestLimit;
        return requestLogStore.ListRecentAsync(limit, cancellationToken);
    }

    public Task<ProxyRequestLog?> GetAsync(string requestId, CancellationToken cancellationToken) =>
        requestLogStore.GetAsync(requestId, cancellationToken);

    public BridgeConfigurationView GetConfiguration()
    {
        var current = runtimeSettingsStore.GetCurrent();
        return new BridgeConfigurationView
        {
            BackendName = current.Backend.Name,
            BackendBaseUrl = current.Backend.BaseUrl,
            ApiKeyHeader = current.Backend.ApiKeyHeader,
            HasApiKey = !string.IsNullOrWhiteSpace(current.Backend.ApiKey),
            DefaultHeaders = new Dictionary<string, string>(current.Backend.DefaultHeaders, StringComparer.OrdinalIgnoreCase),
            ConnectionString = current.Storage.ConnectionString,
            RecentRequestLimit = current.Storage.RecentRequestLimit
        };
    }

    public BridgeConfigurationView UpdateConfiguration(BridgeConfigurationUpdate update)
    {
        var current = runtimeSettingsStore.GetCurrent();
        var next = new BridgeRuntimeOptions
        {
            Backend = new BridgeBackendOptions
            {
                Name = string.IsNullOrWhiteSpace(update.BackendName) ? current.Backend.Name : update.BackendName,
                BaseUrl = string.IsNullOrWhiteSpace(update.BackendBaseUrl) ? current.Backend.BaseUrl : update.BackendBaseUrl,
                ApiKeyHeader = string.IsNullOrWhiteSpace(update.ApiKeyHeader) ? current.Backend.ApiKeyHeader : update.ApiKeyHeader,
                ApiKey = update.ApiKey ?? current.Backend.ApiKey,
                DefaultHeaders = update.DefaultHeaders is null
                    ? new Dictionary<string, string>(current.Backend.DefaultHeaders, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(update.DefaultHeaders, StringComparer.OrdinalIgnoreCase)
            },
            Storage = new BridgeStorageOptions
            {
                ConnectionString = string.IsNullOrWhiteSpace(update.ConnectionString) ? current.Storage.ConnectionString : update.ConnectionString,
                RecentRequestLimit = update.RecentRequestLimit is > 0 ? update.RecentRequestLimit.Value : current.Storage.RecentRequestLimit
            }
        };

        runtimeSettingsStore.Update(next);
        return GetConfiguration();
    }

    public async Task PublishChunkAsync(string requestId, ReadOnlyMemory<byte> chunk, CancellationToken cancellationToken)
    {
        if (chunk.IsEmpty)
        {
            return;
        }

        var content = Encoding.UTF8.GetString(chunk.Span);
        foreach (var transform in responseTransforms)
        {
            content = await transform.TransformAsync(content, cancellationToken);
        }

        await proxyEventSink.ResponseChunkAsync(
            new ProxyResponseChunk
            {
                RequestId = requestId,
                Content = content,
                TimestampUtc = timeProvider.GetUtcNow()
            },
            cancellationToken);
    }

    public async Task CompleteAsync(
        ProxyForwardSession session,
        string responseHeaders,
        string responseBody,
        CancellationToken cancellationToken)
    {
        var duration = Stopwatch.GetElapsedTime(session.StartedTimestamp);
        var statusCode = (int)session.UpstreamResponse.StatusCode;

        await requestLogStore.CompleteAsync(
            session.RequestId,
            statusCode,
            responseHeaders,
            responseBody,
            (long)duration.TotalMilliseconds,
            cancellationToken);

        await proxyEventSink.RequestCompletedAsync(
            new ProxyRequestSummary
            {
                Id = session.RequestId,
                Method = session.Method,
                Path = session.Path,
                StartedAtUtc = session.StartedAtUtc,
                Status = "Completed",
                BackendName = session.BackendName,
                ResponseStatusCode = statusCode,
                DurationMs = (long)duration.TotalMilliseconds
            },
            cancellationToken);
    }

    public async Task FailAsync(
        string requestId,
        DateTimeOffset startedAtUtc,
        long startedTimestamp,
        string method,
        string path,
        string backendName,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var duration = Stopwatch.GetElapsedTime(startedTimestamp);
        await requestLogStore.FailAsync(
            requestId,
            exception.Message,
            (long)duration.TotalMilliseconds,
            cancellationToken);

        await proxyEventSink.RequestCompletedAsync(
            new ProxyRequestSummary
            {
                Id = requestId,
                Method = method,
                Path = path,
                StartedAtUtc = startedAtUtc,
                Status = "Failed",
                BackendName = backendName,
                DurationMs = (long)duration.TotalMilliseconds
            },
            cancellationToken);
    }

    private static ProxyRequestSummary ToSummary(ProxyRequestLog log) => new()
    {
        Id = log.Id,
        Method = log.Method,
        Path = log.Path,
        StartedAtUtc = log.StartedAtUtc,
        Status = log.Status,
        BackendName = log.BackendName,
        ResponseStatusCode = log.ResponseStatusCode,
        DurationMs = log.DurationMs
    };
}

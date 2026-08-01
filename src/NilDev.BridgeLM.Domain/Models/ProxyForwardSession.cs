namespace NilDev.BridgeLM.Domain.Models;

public sealed class ProxyForwardSession : IAsyncDisposable
{
    public required string RequestId { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public required long StartedTimestamp { get; init; }

    public required string Method { get; init; }

    public required string Path { get; init; }

    public required string BackendName { get; init; }

    public required string BackendUrl { get; init; }

    public required HttpResponseMessage UpstreamResponse { get; init; }

    public ValueTask DisposeAsync()
    {
        UpstreamResponse.Dispose();
        return ValueTask.CompletedTask;
    }
}

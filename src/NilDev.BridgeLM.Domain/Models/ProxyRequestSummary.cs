namespace NilDev.BridgeLM.Domain.Models;

public sealed class ProxyRequestSummary
{
    public required string Id { get; init; }

    public required string Method { get; init; }

    public required string Path { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public required string Status { get; init; }

    public required string BackendName { get; init; }

    public int? ResponseStatusCode { get; init; }

    public long? DurationMs { get; init; }
}

namespace NilDev.BridgeLM.Domain.Models;

public sealed class ProxyRequestLog
{
    public required string Id { get; init; }

    public required string Method { get; init; }

    public required string Path { get; init; }

    public required string QueryString { get; init; }

    public required string RequestHeaders { get; init; }

    public required string RequestBody { get; init; }

    public required string BackendName { get; init; }

    public required string BackendUrl { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public string Status { get; init; } = "Pending";

    public int? ResponseStatusCode { get; init; }

    public string? ResponseHeaders { get; init; }

    public string? ResponseBody { get; init; }

    public long? DurationMs { get; init; }

    public string? Error { get; init; }
}

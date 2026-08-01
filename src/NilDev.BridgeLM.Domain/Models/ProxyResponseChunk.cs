namespace NilDev.BridgeLM.Domain.Models;

public sealed class ProxyResponseChunk
{
    public required string RequestId { get; init; }

    public required string Content { get; init; }

    public required DateTimeOffset TimestampUtc { get; init; }
}

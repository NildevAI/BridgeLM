namespace NilDev.BridgeLM.Domain.Models;

public sealed class ProxyInboundRequest
{
    public required string Method { get; init; }

    public required string Path { get; init; }

    public required string QueryString { get; init; }

    public required string ContentType { get; init; }

    public required byte[] Body { get; init; }

    public required Dictionary<string, string[]> Headers { get; init; }
}

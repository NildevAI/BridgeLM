namespace NilDev.BridgeLM.Domain.Abstractions;

using NilDev.BridgeLM.Domain.Models;

public interface IProxyEventSink
{
    Task RequestStartedAsync(ProxyRequestSummary summary, CancellationToken cancellationToken);

    Task ResponseChunkAsync(ProxyResponseChunk chunk, CancellationToken cancellationToken);

    Task RequestCompletedAsync(ProxyRequestSummary summary, CancellationToken cancellationToken);
}

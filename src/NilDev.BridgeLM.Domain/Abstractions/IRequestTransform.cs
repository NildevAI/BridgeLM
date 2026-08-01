namespace NilDev.BridgeLM.Domain.Abstractions;

using NilDev.BridgeLM.Domain.Models;

public interface IRequestTransform
{
    ValueTask<ProxyInboundRequest> TransformAsync(ProxyInboundRequest request, CancellationToken cancellationToken);
}

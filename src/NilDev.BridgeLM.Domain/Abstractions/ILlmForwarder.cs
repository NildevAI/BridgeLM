namespace NilDev.BridgeLM.Domain.Abstractions;

using NilDev.BridgeLM.Domain.Models;

public interface ILlmForwarder
{
    Task<HttpResponseMessage> SendAsync(
        BridgeBackendOptions backend,
        ProxyInboundRequest request,
        CancellationToken cancellationToken);
}

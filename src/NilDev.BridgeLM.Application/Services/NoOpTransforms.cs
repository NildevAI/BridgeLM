namespace NilDev.BridgeLM.Application.Services;

using NilDev.BridgeLM.Domain.Abstractions;
using NilDev.BridgeLM.Domain.Models;

public sealed class NoOpRequestTransform : IRequestTransform
{
    public ValueTask<ProxyInboundRequest> TransformAsync(ProxyInboundRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(request);
}

public sealed class NoOpResponseTransform : IResponseTransform
{
    public ValueTask<string> TransformAsync(string responseBody, CancellationToken cancellationToken) =>
        ValueTask.FromResult(responseBody);
}

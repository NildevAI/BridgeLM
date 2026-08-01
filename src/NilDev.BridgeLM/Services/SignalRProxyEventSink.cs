namespace NilDev.BridgeLM.Services;

using Microsoft.AspNetCore.SignalR;
using NilDev.BridgeLM.Domain.Abstractions;
using NilDev.BridgeLM.Domain.Models;
using NilDev.BridgeLM.Hubs;

public sealed class SignalRProxyEventSink(IHubContext<BridgeHub> hubContext) : IProxyEventSink
{
    public Task RequestStartedAsync(ProxyRequestSummary summary, CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync("requestStarted", summary, cancellationToken);

    public Task ResponseChunkAsync(ProxyResponseChunk chunk, CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync("responseChunk", chunk, cancellationToken);

    public Task RequestCompletedAsync(ProxyRequestSummary summary, CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync("requestCompleted", summary, cancellationToken);
}

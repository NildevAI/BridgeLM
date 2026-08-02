namespace NilDev.BridgeLM.Domain.Abstractions;

using NilDev.BridgeLM.Domain.Models;

public interface IRequestLogStore
{
    Task InitializeAsync(CancellationToken cancellationToken);

    Task AddAsync(ProxyRequestLog log, CancellationToken cancellationToken);

    Task CompleteAsync(
        string requestId,
        int statusCode,
        string responseHeaders,
        string responseBody,
        long durationMs,
        CancellationToken cancellationToken);

    Task FailAsync(
        string requestId,
        string error,
        long durationMs,
        CancellationToken cancellationToken);

    Task<ProxyRequestLog?> GetAsync(string requestId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProxyRequestSummary>> ListRecentAsync(int limit, CancellationToken cancellationToken);

    Task DeleteAsync(string requestId, CancellationToken cancellationToken);

    Task TruncateAsync(CancellationToken cancellationToken);
}

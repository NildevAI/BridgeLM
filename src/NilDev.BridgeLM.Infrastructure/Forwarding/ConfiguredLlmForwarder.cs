namespace NilDev.BridgeLM.Infrastructure.Forwarding;

using System.Net.Http.Headers;
using NilDev.BridgeLM.Domain.Abstractions;
using NilDev.BridgeLM.Domain.Models;

public sealed class ConfiguredLlmForwarder(HttpClient httpClient) : ILlmForwarder
{
    private static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection",
        "Host",
        "Keep-Alive",
        "Proxy-Authenticate",
        "Proxy-Authorization",
        "TE",
        "Trailer",
        "Transfer-Encoding",
        "Upgrade"
    };

    public async Task<HttpResponseMessage> SendAsync(
        BridgeBackendOptions backend,
        ProxyInboundRequest request,
        CancellationToken cancellationToken)
    {
        var baseUri = new Uri(EnsureTrailingSlash(backend.BaseUrl), UriKind.Absolute);
        var targetUri = new Uri(baseUri, request.Path.TrimStart('/') + request.QueryString);

        using var outboundRequest = new HttpRequestMessage(new HttpMethod(request.Method), targetUri);
        if (request.Body.Length > 0)
        {
            outboundRequest.Content = new ByteArrayContent(request.Body);
            if (!string.IsNullOrWhiteSpace(request.ContentType))
            {
                outboundRequest.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType);
            }
        }

        foreach (var header in request.Headers)
        {
            if (HopByHopHeaders.Contains(header.Key))
            {
                continue;
            }

            if (!outboundRequest.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                outboundRequest.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in backend.DefaultHeaders)
        {
            outboundRequest.Headers.Remove(header.Key);
            outboundRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (!string.IsNullOrWhiteSpace(backend.ApiKey))
        {
            outboundRequest.Headers.Remove(backend.ApiKeyHeader);
            outboundRequest.Headers.TryAddWithoutValidation(backend.ApiKeyHeader, backend.ApiKey);
        }

        return await httpClient.SendAsync(outboundRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static string EnsureTrailingSlash(string value) => value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";
}

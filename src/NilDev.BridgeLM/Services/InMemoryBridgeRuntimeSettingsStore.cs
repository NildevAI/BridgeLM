namespace NilDev.BridgeLM.Services;

using Microsoft.Extensions.Options;
using NilDev.BridgeLM.Domain.Abstractions;
using NilDev.BridgeLM.Domain.Models;

public sealed class InMemoryBridgeRuntimeSettingsStore(IOptions<BridgeRuntimeOptions> options) : IBridgeRuntimeSettingsStore
{
    private readonly Lock gate = new();
    private BridgeRuntimeOptions current = Clone(options.Value);

    public BridgeRuntimeOptions GetCurrent()
    {
        lock (gate)
        {
            return Clone(current);
        }
    }

    public void Update(BridgeRuntimeOptions options)
    {
        lock (gate)
        {
            current = Clone(options);
        }
    }

    private static BridgeRuntimeOptions Clone(BridgeRuntimeOptions options) => new()
    {
        Backend = new BridgeBackendOptions
        {
            Name = options.Backend.Name,
            BaseUrl = options.Backend.BaseUrl,
            ApiKeyHeader = options.Backend.ApiKeyHeader,
            ApiKey = options.Backend.ApiKey,
            DefaultHeaders = new Dictionary<string, string>(options.Backend.DefaultHeaders, StringComparer.OrdinalIgnoreCase)
        },
        Storage = new BridgeStorageOptions
        {
            ConnectionString = options.Storage.ConnectionString,
            RecentRequestLimit = options.Storage.RecentRequestLimit
        }
    };
}

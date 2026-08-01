namespace NilDev.BridgeLM.Domain.Models;

public sealed class BridgeRuntimeOptions
{
    public const string SectionName = "Bridge";

    public BridgeBackendOptions Backend { get; set; } = new();

    public BridgeStorageOptions Storage { get; set; } = new();
}

public sealed class BridgeBackendOptions
{
    public string Name { get; set; } = "Default";

    public string BaseUrl { get; set; } = "http://localhost:11434";

    public string ApiKeyHeader { get; set; } = "Authorization";

    public string ApiKey { get; set; } = string.Empty;

    public Dictionary<string, string> DefaultHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class BridgeStorageOptions
{
    public string ConnectionString { get; set; } = "Data Source=data/bridgelm.db";

    public int RecentRequestLimit { get; set; } = 100;
}

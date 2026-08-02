namespace NilDev.BridgeLM.Domain.Models;

public sealed class BridgeConfigurationView
{
    public required string BackendName { get; init; }

    public required string BackendBaseUrl { get; init; }

    public required string ApiKeyHeader { get; init; }

    public required bool HasApiKey { get; init; }

    public required Dictionary<string, string> DefaultHeaders { get; init; }

    public required string ConnectionString { get; init; }

    public required int RecentRequestLimit { get; init; }
}

public sealed class BridgeConfigurationUpdate
{
    public string? BackendName { get; init; }

    public string? BackendBaseUrl { get; init; }

    public string? ApiKeyHeader { get; init; }

    public string? ApiKey { get; init; }

    public Dictionary<string, string>? DefaultHeaders { get; init; }

    public string? ConnectionString { get; init; }

    public int? RecentRequestLimit { get; init; }
}

public sealed class BridgeNamedConfigurationSummary
{
    public required string Name { get; init; }

    public required string BackendName { get; init; }

    public required string BackendBaseUrl { get; init; }

    public required bool HasApiKey { get; init; }

    public required int RecentRequestLimit { get; init; }

    public required bool IsActive { get; init; }
}

public sealed class BridgeNamedConfigurationView
{
    public required string Name { get; init; }

    public required BridgeConfigurationView Configuration { get; init; }

    public required bool IsActive { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed class BridgeNamedConfigurationCreate
{
    public required string Name { get; init; }

    public string? CopyFromName { get; init; }

    public string? BackendName { get; init; }

    public string? BackendBaseUrl { get; init; }

    public string? ApiKeyHeader { get; init; }

    public string? ApiKey { get; init; }

    public Dictionary<string, string>? DefaultHeaders { get; init; }

    public string? ConnectionString { get; init; }

    public int? RecentRequestLimit { get; init; }
}

public sealed class BridgeDuplicateConfigurationRequest
{
    public required string Name { get; init; }
}

public sealed class BridgeRenameConfigurationRequest
{
    public required string Name { get; init; }
}

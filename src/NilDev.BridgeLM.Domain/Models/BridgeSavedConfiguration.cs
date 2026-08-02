namespace NilDev.BridgeLM.Domain.Models;

public sealed class BridgeSavedConfiguration
{
    public required string Name { get; init; }

    public required BridgeRuntimeOptions Options { get; init; }

    public required bool IsActive { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required DateTimeOffset UpdatedAtUtc { get; init; }
}
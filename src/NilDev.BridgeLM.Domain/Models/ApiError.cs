namespace NilDev.BridgeLM.Domain.Models;

public sealed class ApiError
{
    public required string Error { get; init; }

    public string? Detail { get; init; }
}

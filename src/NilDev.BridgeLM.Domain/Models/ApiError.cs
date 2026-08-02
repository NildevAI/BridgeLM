using System.Collections.Generic;

namespace NilDev.BridgeLM.Domain.Models;

public sealed class ApiError
{
    public required string Error { get; init; }

    public string? Detail { get; init; }

    public List<string>? FormErrors { get; init; }

    public Dictionary<string, string[]>? FieldErrors { get; init; }
}

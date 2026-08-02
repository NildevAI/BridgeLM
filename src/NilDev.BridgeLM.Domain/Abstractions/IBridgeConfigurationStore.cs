namespace NilDev.BridgeLM.Domain.Abstractions;

using NilDev.BridgeLM.Domain.Models;

public interface IBridgeConfigurationStore
{
    Task EnsureInitializedAsync(BridgeSavedConfiguration seedConfiguration, CancellationToken cancellationToken);

    Task<IReadOnlyList<BridgeSavedConfiguration>> ListAsync(CancellationToken cancellationToken);

    Task<BridgeSavedConfiguration?> GetAsync(string name, CancellationToken cancellationToken);

    Task CreateOrUpdateAsync(BridgeSavedConfiguration configuration, CancellationToken cancellationToken);

    Task<bool> RenameAsync(string currentName, string newName, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string name, CancellationToken cancellationToken);

    Task<bool> SetActiveAsync(string name, CancellationToken cancellationToken);
}
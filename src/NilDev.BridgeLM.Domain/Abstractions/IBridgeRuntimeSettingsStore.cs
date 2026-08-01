namespace NilDev.BridgeLM.Domain.Abstractions;

using NilDev.BridgeLM.Domain.Models;

public interface IBridgeRuntimeSettingsStore
{
    BridgeRuntimeOptions GetCurrent();

    void Update(BridgeRuntimeOptions options);
}

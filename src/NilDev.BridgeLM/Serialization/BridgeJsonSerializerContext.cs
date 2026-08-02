namespace NilDev.BridgeLM.Serialization;

using System.Text.Json;
using System.Text.Json.Serialization;
using NilDev.BridgeLM.Domain.Models;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ApiError))]
[JsonSerializable(typeof(BridgeDuplicateConfigurationRequest))]
[JsonSerializable(typeof(BridgeNamedConfigurationCreate))]
[JsonSerializable(typeof(BridgeNamedConfigurationSummary))]
[JsonSerializable(typeof(BridgeNamedConfigurationView))]
[JsonSerializable(typeof(BridgeConfigurationUpdate))]
[JsonSerializable(typeof(BridgeConfigurationView))]
[JsonSerializable(typeof(BridgeRenameConfigurationRequest))]
[JsonSerializable(typeof(Dictionary<string, string[]>))]
[JsonSerializable(typeof(List<BridgeNamedConfigurationSummary>))]
[JsonSerializable(typeof(ProxyRequestLog))]
[JsonSerializable(typeof(ProxyRequestSummary))]
[JsonSerializable(typeof(ProxyResponseChunk))]
[JsonSerializable(typeof(List<ProxyRequestSummary>))]
internal partial class BridgeJsonSerializerContext : JsonSerializerContext
{
}

namespace NilDev.BridgeLM.IntegrationTests.Serialization;

using System.Text.Json;
using System.Text.Json.Serialization;
using NilDev.BridgeLM.Domain.Models;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(BridgeNamedConfigurationCreate))]
[JsonSerializable(typeof(BridgeNamedConfigurationSummary))]
[JsonSerializable(typeof(BridgeNamedConfigurationView))]
[JsonSerializable(typeof(BridgeConfigurationView))]
[JsonSerializable(typeof(BridgeDuplicateConfigurationRequest))]
[JsonSerializable(typeof(List<ProxyRequestSummary>))]
[JsonSerializable(typeof(ProxyRequestLog))]
[JsonSerializable(typeof(List<BridgeNamedConfigurationSummary>))]
[JsonSerializable(typeof(BridgeRenameConfigurationRequest))]
internal partial class IntegrationTestJsonContext : JsonSerializerContext
{
}

namespace NilDev.BridgeLM.Serialization;

using System.Text.Json;
using System.Text.Json.Serialization;
using NilDev.BridgeLM.Domain.Models;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ApiError))]
[JsonSerializable(typeof(BridgeConfigurationUpdate))]
[JsonSerializable(typeof(BridgeConfigurationView))]
[JsonSerializable(typeof(Dictionary<string, string[]>))]
[JsonSerializable(typeof(ProxyRequestLog))]
[JsonSerializable(typeof(ProxyRequestSummary))]
[JsonSerializable(typeof(List<ProxyRequestSummary>))]
internal partial class BridgeJsonSerializerContext : JsonSerializerContext
{
}

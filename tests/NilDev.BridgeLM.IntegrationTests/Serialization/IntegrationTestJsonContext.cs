namespace NilDev.BridgeLM.IntegrationTests.Serialization;

using System.Text.Json;
using System.Text.Json.Serialization;
using NilDev.BridgeLM.Domain.Models;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(BridgeConfigurationView))]
[JsonSerializable(typeof(List<ProxyRequestSummary>))]
[JsonSerializable(typeof(ProxyRequestLog))]
internal partial class IntegrationTestJsonContext : JsonSerializerContext
{
}

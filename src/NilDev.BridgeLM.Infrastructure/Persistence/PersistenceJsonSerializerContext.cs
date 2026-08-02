namespace NilDev.BridgeLM.Infrastructure.Persistence;

using System.Text.Json;
using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class PersistenceJsonSerializerContext : JsonSerializerContext
{
}
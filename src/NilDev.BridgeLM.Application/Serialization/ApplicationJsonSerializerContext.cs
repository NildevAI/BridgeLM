namespace NilDev.BridgeLM.Application.Serialization;

using System.Text.Json;
using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(Dictionary<string, string[]>))]
internal partial class ApplicationJsonSerializerContext : JsonSerializerContext
{
}

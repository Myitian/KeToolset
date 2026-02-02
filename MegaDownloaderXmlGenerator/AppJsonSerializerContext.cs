using System.Text.Json.Serialization;

namespace MegaDownloaderXmlGenerator;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(MegaUtils.EntryList))]
[JsonSerializable(typeof(MegaUtils.EntryName))]
sealed partial class AppJsonSerializerContext : JsonSerializerContext;
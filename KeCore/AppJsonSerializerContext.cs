using KeCore.API;
using System.Text.Json.Serialization;

namespace KeCore;

[JsonSerializable(typeof(Archive))]
[JsonSerializable(typeof(PostRoot))]
[JsonSerializable(typeof(PostResult))]
sealed partial class AppJsonSerializerContext : JsonSerializerContext;
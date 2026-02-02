using KeCore.API;
using System.Text.Json.Serialization;

namespace KeCore;

[JsonSerializable(typeof(Archive))]
[JsonSerializable(typeof(PostRoot))]
[JsonSerializable(typeof(PostsResult))]
sealed partial class AppJsonSerializerContext : JsonSerializerContext;
using KeCore.API;
using System.Text.Json.Serialization;

namespace KeCore;

[JsonSerializable(typeof(Archive))]
[JsonSerializable(typeof(PostRoot))]
[JsonSerializable(typeof(PostResult))]
[JsonSerializable(typeof(List<Comment>))]
sealed partial class AppJsonSerializerContext : JsonSerializerContext;
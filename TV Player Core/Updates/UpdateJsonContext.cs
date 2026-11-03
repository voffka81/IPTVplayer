using System.Text.Json.Serialization;

namespace TV_Player.Updates;

// Source-generated metadata remains available in trimmed Android Release builds.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(List<AppRelease>))]
[JsonSerializable(typeof(AndroidUpdateManifest))]
internal partial class UpdateJsonContext : JsonSerializerContext;

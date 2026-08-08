using System.Text.Json.Serialization;

namespace Mfc.RouterOs.Capabilities;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(CompatibilityManifestDocument))]
internal sealed partial class CompatibilityManifestJsonContext : JsonSerializerContext;

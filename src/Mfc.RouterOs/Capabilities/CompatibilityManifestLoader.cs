using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.RouterOs.Capabilities;

/// <summary>Loads the embedded, versioned compatibility manifest (Adapter Spec §38.1).</summary>
public static class CompatibilityManifestLoader
{
    public const string ResourceName = "Mfc.RouterOs.Capabilities.Manifests.compatibility-manifest.v1.json";

    public const int ExpectedSchemaVersion = 1;

    private static readonly Lazy<(CompatibilityManifestDocument Document, Hash256 Hash)> Cached = new(LoadEmbedded);

    public static CompatibilityManifestDocument Load() => Cached.Value.Document;

    public static Hash256 ManifestHash => Cached.Value.Hash;

    private static (CompatibilityManifestDocument Document, Hash256 Hash) LoadEmbedded()
    {
        Assembly assembly = typeof(CompatibilityManifestLoader).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            string available = string.Join(", ", assembly.GetManifestResourceNames());
            throw new InvalidOperationException(
                $"Embedded compatibility manifest '{ResourceName}' was not found. Available: {available}");
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        byte[] bytes = memory.ToArray();
        Hash256 hash = Hash256.Create(SHA256.HashData(bytes));

        CompatibilityManifestDocument document = JsonSerializer.Deserialize(
            Encoding.UTF8.GetString(bytes),
            CompatibilityManifestJsonContext.Default.CompatibilityManifestDocument)
            ?? throw new InvalidOperationException("Compatibility manifest deserialized to null.");

        if (document.SchemaVersion != ExpectedSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported compatibility manifest schemaVersion {document.SchemaVersion}; expected {ExpectedSchemaVersion}.");
        }

        if (string.IsNullOrWhiteSpace(document.ProfileId))
        {
            throw new InvalidOperationException("Compatibility manifest profileId is required.");
        }

        ValidateNonEmpty(document.SupportedRouterOsBuilds, nameof(document.SupportedRouterOsBuilds));
        ValidateNonEmpty(document.Architectures, nameof(document.Architectures));
        ValidateNonEmpty(document.BoardClasses, nameof(document.BoardClasses));
        ValidateNonEmpty(document.RequiredMenus, nameof(document.RequiredMenus));
        ValidateNonEmpty(document.RequiredProperties, nameof(document.RequiredProperties));
        return (document, hash);
    }

    private static void ValidateNonEmpty(IReadOnlyList<string> values, string name)
    {
        if (values.Count == 0 || values.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException($"Compatibility manifest '{name}' must be non-empty.");
        }
    }
}

using Mfc.Domain.Canonicalization;

namespace Mfc.Application.Mapping;

/// <summary>
/// Last-capture facts for DeviceView: VRRP observation labels plus system.resource version/board-name.
/// Does not invent Master/Backup or reachability.
/// </summary>
public sealed class DeviceLastCaptureFacts
{
    public static DeviceLastCaptureFacts Empty { get; } = new()
    {
        VrrpRoleLabels = [],
        RouterOsVersion = null,
        Model = null,
    };

    public required IReadOnlyList<string> VrrpRoleLabels { get; init; }

    public string? RouterOsVersion { get; init; }

    public string? Model { get; init; }

    public static DeviceLastCaptureFacts FromCanonicalSections(IReadOnlyList<CanonicalSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        string? version = null;
        string? model = null;
        foreach (CanonicalSection section in sections)
        {
            if (!string.Equals(section.SectionId, CanonicalSectionIds.SystemResource, StringComparison.Ordinal)
                || section.Domain != CanonicalDomain.Observations)
            {
                continue;
            }

            foreach (CanonicalRecord record in section.Records)
            {
                if (version is null
                    && record.Properties.TryGetValue("version", out string? rawVersion)
                    && !string.IsNullOrWhiteSpace(rawVersion))
                {
                    version = rawVersion.Trim();
                }

                if (model is null
                    && record.Properties.TryGetValue("board-name", out string? boardName)
                    && !string.IsNullOrWhiteSpace(boardName))
                {
                    model = boardName.Trim();
                }
            }
        }

        return new DeviceLastCaptureFacts
        {
            VrrpRoleLabels = DeviceVrrpRoleLabelProjector.FromCanonicalSections(sections),
            RouterOsVersion = version,
            Model = model,
        };
    }
}

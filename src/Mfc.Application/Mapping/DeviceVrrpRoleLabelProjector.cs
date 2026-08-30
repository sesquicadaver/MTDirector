using Mfc.Domain.Canonicalization;

namespace Mfc.Application.Mapping;

/// <summary>
/// Projects last-capture <c>ha.vrrp</c> observation records onto DeviceView labels.
/// Does not invent roles: empty observations stay empty (no placeholder Master/Backup).
/// </summary>
public static class DeviceVrrpRoleLabelProjector
{
    public static IReadOnlyList<string> FromCanonicalSections(IReadOnlyList<CanonicalSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        HashSet<string> labels = new(StringComparer.Ordinal);
        foreach (CanonicalSection section in sections)
        {
            if (!string.Equals(section.SectionId, CanonicalSectionIds.HaVrrp, StringComparison.Ordinal)
                || section.Domain != CanonicalDomain.Observations)
            {
                continue;
            }

            foreach (CanonicalRecord record in section.Records)
            {
                if (!record.Properties.TryGetValue("role", out string? role)
                    || string.IsNullOrWhiteSpace(role))
                {
                    continue;
                }

                record.Properties.TryGetValue("group", out string? group);
                string trimmedRole = role.Trim();
                string label = string.IsNullOrWhiteSpace(group)
                    ? trimmedRole
                    : $"{trimmedRole} · {group.Trim()}";
                labels.Add(label);
            }
        }

        return labels.Count == 0
            ? []
            : labels.OrderBy(static l => l, StringComparer.Ordinal).ToArray();
    }
}

using System.Collections.Immutable;
using Mfc.Domain.Canonicalization;

namespace Mfc.Domain.Policy;

/// <summary>
/// Writes exact MFC-CJ1 canonical RouterOS filter artifact bytes (Compiler Spec §24).
/// Property order is schema-fixed; no whitespace; UTF-8 without BOM/trailing newline.
/// </summary>
public static class RouterOsFilterArtifactCanonicalWriter
{
    public const string SchemaName = "mfc.routeros-filter-artifact/1";

    public static byte[] Write(RouterOsFilterArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        CanonicalJsonWriter writer = new();
        writer.WriteObject(
        [
            ("schema", w => w.WriteString(SchemaName)),
            ("layoutVersion", w => w.WriteString(artifact.LayoutVersion)),
            ("artifactId", w => w.WriteString(artifact.ArtifactId)),
            ("addressLists", w => WriteAddressLists(w, artifact.AddressLists)),
            ("chains", w => WriteChains(w, artifact.Chains)),
            ("anchors", w => WriteAnchors(w, artifact.AnchorTargets)),
        ]);
        return writer.ToUtf8Bytes();
    }

    private static void WriteAddressLists(CanonicalJsonWriter writer, ImmutableArray<AddressListArtifact> lists)
    {
        writer.WriteArrayStart();
        for (int i = 0; i < lists.Length; i++)
        {
            if (i > 0)
            {
                writer.WriteComma();
            }

            AddressListArtifact list = lists[i];
            writer.WriteObject(
            [
                ("family", w => w.WriteString(RouterOsFilterArtifactIdentity.FormatFamily(list.Family))),
                ("name", w => w.WriteString(list.Name)),
                ("contentHash", w => w.WriteString(list.ContentHash.ToString())),
                ("entries", w =>
                {
                    w.WriteArrayStart();
                    for (int e = 0; e < list.Entries.Length; e++)
                    {
                        if (e > 0)
                        {
                            w.WriteComma();
                        }

                        w.WriteObject([("address", x => x.WriteString(list.Entries[e].Address))]);
                    }

                    w.WriteArrayEnd();
                }),
            ]);
        }

        writer.WriteArrayEnd();
    }

    private static void WriteChains(CanonicalJsonWriter writer, ImmutableArray<ChainArtifact> chains)
    {
        writer.WriteArrayStart();
        for (int i = 0; i < chains.Length; i++)
        {
            if (i > 0)
            {
                writer.WriteComma();
            }

            ChainArtifact chain = chains[i];
            writer.WriteObject(
            [
                ("family", w => w.WriteString(RouterOsFilterArtifactIdentity.FormatFamily(chain.Family))),
                ("builtInContext", w => w.WriteString(RouterOsFilterArtifactIdentity.FormatBuiltIn(chain.BuiltInContext))),
                ("name", w => w.WriteString(chain.Name)),
                ("role", w => w.WriteString(RouterOsFilterArtifactIdentity.FormatRole(chain.Role))),
                ("rules", w => WriteRules(w, chain.Rules)),
            ]);
        }

        writer.WriteArrayEnd();
    }

    private static void WriteRules(CanonicalJsonWriter writer, ImmutableArray<FilterRuleArtifact> rules)
    {
        writer.WriteArrayStart();
        for (int i = 0; i < rules.Length; i++)
        {
            if (i > 0)
            {
                writer.WriteComma();
            }

            FilterRuleArtifact rule = rules[i];
            List<(string Key, Action<CanonicalJsonWriter> WriteValue)> properties =
            [
                ("ordinal", w => w.WriteNumber(rule.Ordinal)),
            ];
            if (rule.LogicalRuleId is Guid logicalRuleId)
            {
                properties.Add(("logicalRuleId", w => w.WriteString(logicalRuleId.ToString("D"))));
            }

            if (rule.VariantIndex is uint variantIndex)
            {
                properties.Add(("variantIndex", w => w.WriteNumber(variantIndex)));
            }

            if (!string.IsNullOrWhiteSpace(rule.StructuralRole))
            {
                properties.Add(("structuralRole", w => w.WriteString(rule.StructuralRole)));
            }

            properties.Add(("matchers", w => WriteSortedMap(w, rule.Matchers)));
            properties.Add(("action", w => w.WriteString(rule.Action)));
            properties.Add(("actionParameters", w => WriteSortedMap(w, rule.ActionParameters)));
            properties.Add(("log", w => w.WriteBoolean(rule.Log)));
            if (!string.IsNullOrWhiteSpace(rule.LogPrefix))
            {
                properties.Add(("logPrefix", w => w.WriteString(rule.LogPrefix)));
            }

            properties.Add(("comment", w => w.WriteString(rule.Comment)));
            writer.WriteObject(properties);
        }

        writer.WriteArrayEnd();
    }

    private static void WriteAnchors(CanonicalJsonWriter writer, ImmutableArray<AnchorTargetArtifact> anchors)
    {
        writer.WriteArrayStart();
        for (int i = 0; i < anchors.Length; i++)
        {
            if (i > 0)
            {
                writer.WriteComma();
            }

            AnchorTargetArtifact anchor = anchors[i];
            writer.WriteObject(
            [
                ("family", w => w.WriteString(RouterOsFilterArtifactIdentity.FormatFamily(anchor.Family))),
                ("builtInChain", w => w.WriteString(RouterOsFilterArtifactIdentity.FormatBuiltIn(anchor.BuiltInChain))),
                ("expectedAnchorComment", w => w.WriteString(anchor.ExpectedAnchorComment)),
                ("desiredJumpTarget", w => w.WriteString(anchor.DesiredJumpTarget)),
            ]);
        }

        writer.WriteArrayEnd();
    }

    private static void WriteSortedMap(CanonicalJsonWriter writer, ImmutableSortedDictionary<string, string> map)
    {
        writer.WriteObjectStart();
        bool first = true;
        foreach ((string key, string value) in map)
        {
            if (!first)
            {
                writer.WriteComma();
            }

            first = false;
            writer.WritePropertyName(key);
            writer.WriteString(value);
        }

        writer.WriteObjectEnd();
    }
}

using System.Text.Json;
using Mfc.Domain.Canonicalization;

namespace Mfc.Domain.Policy;

/// <summary>
/// Writes exact MFC-CJ1 canonical policy revision bytes (Policy Model §33).
/// Property order is schema-fixed; no whitespace; UTF-8 without BOM/trailing newline.
/// </summary>
public static class PolicyCanonicalWriter
{
    public static byte[] Write(PolicyDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        CanonicalJsonWriter writer = new();
        writer.WriteObject(
        [
            ("schema", w => w.WriteString(PolicyDocument.SchemaName)),
            ("schema_version", w => w.WriteNumber(document.SchemaVersion)),
            ("policy_kind", w => w.WriteString(FormatKind(document.Kind))),
            ("owner_scope", w => w.WriteString(FormatOwnerScope(document.OwnerScope))),
            ("chain_contracts", w => WriteElementArray(w, document.ChainContracts)),
            ("zone_definitions", w => WriteElementArray(w, document.ZoneDefinitions)),
            ("address_objects", w => WriteElementArray(w, document.AddressObjects)),
            ("service_objects", w => WriteElementArray(w, document.ServiceObjects)),
            ("rules", w => WriteElementArray(w, document.Rules)),
            ("tests", w => WriteElementArray(w, document.Tests)),
            ("exception_metadata", w => w.WriteSortedObject(document.ExceptionMetadata)),
        ]);
        return writer.ToUtf8Bytes();
    }

    public static string FormatKind(PolicyKind kind)
        => kind switch
        {
            PolicyKind.CompanyBaseline => "COMPANY_BASELINE",
            PolicyKind.SiteOverlay => "SITE_OVERLAY",
            PolicyKind.NodeOverlay => "NODE_OVERLAY",
            PolicyKind.Exception => "EXCEPTION",
            _ => throw new DomainInvariantException($"Unknown policy kind '{kind}'."),
        };

    public static string FormatOwnerScope(PolicyOwnerScope scope)
        => scope switch
        {
            PolicyOwnerScope.Company => "COMPANY",
            PolicyOwnerScope.Site => "SITE",
            PolicyOwnerScope.Node => "NODE",
            _ => throw new DomainInvariantException($"Unknown owner scope '{scope}'."),
        };

    public static string FormatRevisionState(PolicyRevisionState state)
        => state switch
        {
            PolicyRevisionState.Draft => "DRAFT",
            PolicyRevisionState.Validated => "VALIDATED",
            PolicyRevisionState.InReview => "IN_REVIEW",
            PolicyRevisionState.Approved => "APPROVED",
            PolicyRevisionState.Rejected => "REJECTED",
            PolicyRevisionState.Superseded => "SUPERSEDED",
            PolicyRevisionState.Revoked => "REVOKED",
            _ => throw new DomainInvariantException($"Unknown revision state '{state}'."),
        };

    private static void WriteElementArray(CanonicalJsonWriter writer, IReadOnlyList<JsonElement> elements)
    {
        writer.WriteArrayStart();
        for (int i = 0; i < elements.Count; i++)
        {
            if (i > 0)
            {
                writer.WriteComma();
            }

            // Opaque elements are re-emitted as compact JSON (no whitespace).
            writer.WriteRaw(elements[i].GetRawText());
        }

        writer.WriteArrayEnd();
    }
}

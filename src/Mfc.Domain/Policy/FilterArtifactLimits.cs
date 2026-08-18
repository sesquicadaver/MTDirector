namespace Mfc.Domain.Policy;

/// <summary>Encoded filter-artifact size limits (Compiler Spec §27 / §28 <c>ARTIFACT_SIZE_LIMIT</c>).</summary>
public static class FilterArtifactLimits
{
    /// <summary>Maximum MFC-CJ1 canonical bytes for one sealed filter artifact (layout v1).</summary>
    public const int LayoutV1MaxCanonicalBytes = 32 * 1024 * 1024;
}

namespace Mfc.Domain.Diff;

/// <summary>Semantic diff domain (Canonical Spec §29.2).</summary>
public enum DiffDomain : byte
{
    Configuration = 0,
    Observation = 1,
    Capability = 2,
    Compatibility = 3,
}

/// <summary>Change flags that may combine on one <see cref="DiffEntry"/> (§29.3).</summary>
public enum DiffChange : byte
{
    Added = 0,
    Removed = 1,
    Modified = 2,
    Moved = 3,
    StateChanged = 4,
    SectionStatusChanged = 5,
}

/// <summary>Match confidence levels; MODIFIED is allowed only for ControllerId/NaturalKey (§29.4).</summary>
public enum MatchConfidence : byte
{
    ControllerId = 0,
    NaturalKey = 1,
    ExactFingerprint = 2,
    ExactSequence = 3,
    Conservative = 4,
}

using BA.Dmo.Domain.Shared.Access;

namespace BA.Dmo.Domain.Modules.JobOn;

/// <summary>
/// Immutable revision snapshot per N05 (TD-18). Each Save creates a NEW revision
/// - never UPDATE of saved revisions. Revision_id is pinned by downstream consumers
/// (Peso, Pegamentos) for historical attribution. image_asset_id is LOGICAL metadata only.
/// </summary>
public sealed record JobOnRevision
{
    /// <summary>Primary key.</summary>
    public Guid JobOnRevisionId { get; init; }

    /// <summary>Parent Job On ID.</summary>
    public Guid JobOnId { get; init; }

    /// <summary>Revision number (>= 1).</summary>
    public int RevisionNumber { get; init; }

    /// <summary>Optional production_snapshot JSON.</summary>
    public string? ProductionSnapshot { get; init; }

    /// <summary>Optional article reference snapshot.</summary>
    public string? ReferenceSnapshot { get; init; }

    /// <summary>Optional machine_snapshot.</summary>
    public string? MachineSnapshot { get; init; }

    /// <summary>Optional dates_snapshot.</summary>
    public string? DatesSnapshot { get; init; }

    /// <summary>JSONB: sections (secções de produção).</summary>
    public string Sections { get; init; } = "{}";

    /// <summary>Optional drop_count (gota).</summary>
    public decimal? DropCount { get; init; }

    /// <summary>Optional type_snapshot.</summary>
    public string? TypeSnapshot { get; init; }

    /// <summary>Optional stop_snapshot.</summary>
    public string? StopSnapshot { get; init; }

    /// <summary>Optional weight_snapshot (peso em gramas).</summary>
    public decimal? WeightSnapshot { get; init; }

    /// <summary>Optional process_snapshot (NNPB/PS from Peso lot).</summary>
    public string? ProcessSnapshot { get; init; }

    /// <summary>General notes field.</summary>
    public string? GeneralNotes { get; init; }

    /// <summary>image_asset_id - LOGICAL metadata ONLY, not binary (TD-23).</summary>
    public string? ImageAssetId { get; init; }

    /// <summary>Change reason (mandatory when editing closed revision).</summary>
    public string? ChangeReason { get; init; }

    /// <summary>Actor who saved this revision.</summary>
    public string? SavedBy { get; init; }

    /// <summary>Saved timestamp.</summary>
    public DateTime SavedAtUtc { get; init; }

    /// <summary>Components collection loaded separately.</summary>
    public IReadOnlyList<JobOnComponent>? Components { get; init; }

    /// <summary>Verifications collection loaded separately.</summary>
    public IReadOnlyList<JobOnVerificationOccurrence>? Verifications { get; init; }

    /// <summary>
    /// Canonical copy: creates a new revision with the next revision number,
    /// a new id, and the supplied overrides. All snapshot fields are preserved
    /// from the source unless explicitly overridden. Components and Verifications
    /// are shared by reference (they are immutable record types).
    /// </summary>
    private JobOnRevision CopyToNextRevision(
        string? generalNotes,
        string? changeReason,
        string? imageAssetId,
        bool clearImageAssetId,
        string? savedBy,
        DateTime savedAtUtc,
        IReadOnlyList<JobOnComponent>? newComponents,
        IReadOnlyList<JobOnVerificationOccurrence>? newVerifications)
    {
        return new JobOnRevision
        {
            JobOnRevisionId = Guid.NewGuid(),
            JobOnId = this.JobOnId,
            RevisionNumber = this.RevisionNumber + 1,
            ProductionSnapshot = this.ProductionSnapshot,
            ReferenceSnapshot = this.ReferenceSnapshot,
            MachineSnapshot = this.MachineSnapshot,
            DatesSnapshot = this.DatesSnapshot,
            Sections = this.Sections,
            DropCount = this.DropCount,
            TypeSnapshot = this.TypeSnapshot,
            StopSnapshot = this.StopSnapshot,
            WeightSnapshot = this.WeightSnapshot,
            ProcessSnapshot = this.ProcessSnapshot,
            GeneralNotes = generalNotes ?? this.GeneralNotes,
            ImageAssetId = clearImageAssetId ? null : (imageAssetId ?? this.ImageAssetId),
            ChangeReason = changeReason,
            SavedBy = savedBy ?? this.SavedBy,
            SavedAtUtc = savedAtUtc,
            Components = newComponents ?? this.Components,
            Verifications = newVerifications ?? this.Verifications
        };
    }

    /// <summary>Clockwise rotation: clone this revision with modifications.</summary>
    public JobOnRevision CloneWithChanges(
        string? generalNotes = null,
        string? changeReason = null,
        string? imageAssetId = null,
        IReadOnlyList<JobOnComponent>? newComponents = null,
        IReadOnlyList<JobOnVerificationOccurrence>? newVerifications = null)
    {
        return CopyToNextRevision(
            generalNotes: generalNotes,
            changeReason: changeReason,
            imageAssetId: imageAssetId,
            clearImageAssetId: false,
            savedBy: this.SavedBy,
            savedAtUtc: DateTime.UtcNow,
            newComponents: newComponents,
            newVerifications: newVerifications);
    }

    /// <summary>
    /// Create a new revision with ImageAssetId explicitly cleared to null (TD-23).
    /// This is the canonical domain operation for removing the image association
    /// from a Job On revision. Unlike CloneWithChanges(imageAssetId: null) which
    /// preserves the current value via null-coalescing, this operation
    /// unambiguously removes the image association. All other snapshot fields
    /// are preserved. Normal duplication always preserves the existing
    /// ImageAssetId because the article does not change.
    /// </summary>
    public JobOnRevision CreateImageRemovalRevision(string savedBy, DateTime savedAtUtc)
    {
        return CopyToNextRevision(
            generalNotes: null,
            changeReason: null,
            imageAssetId: null,
            clearImageAssetId: true,
            savedBy: savedBy,
            savedAtUtc: savedAtUtc,
            newComponents: null,
            newVerifications: null);
    }
}
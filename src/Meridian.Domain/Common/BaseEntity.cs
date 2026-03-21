namespace Meridian.Domain.Common;

/// <summary>
/// every entity in the system (Judge, CourtCase, DetentionFacility, etc.)
/// needs the same basic fields: a unique ID, when it was created,
/// and when it was last updated. Rather than copy-pasting those three properties
/// onto every single class, we put them here once and every entity inherits them.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }

    public void SetUpdated() => UpdatedAt = DateTime.UtcNow;
}

/// <summary>
/// extends further for entities where we also want to track who made changes
/// (user ID), not just when. We'll use that for things like alert rules where
/// knowing which attorney created it matters.
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}
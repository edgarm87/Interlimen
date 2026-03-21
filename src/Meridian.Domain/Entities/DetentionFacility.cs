using Meridian.Domain.Common;

namespace Meridian.Domain.Entities;

public class DetentionFacility : BaseEntity
{
    public string Name { get; private set; } = default!;
    public string? AffNumber { get; private set; }
    public string City { get; private set; } = default!;
    public string State { get; private set; } = default!;
    public FacilityType Type { get; private set; }
    public int? RatedCapacity { get; private set; }
    public bool IsActive { get; private set; } = true;

    public ICollection<DetentionPopulationSnapshot> PopulationSnapshots { get; private set; }
        = new List<DetentionPopulationSnapshot>();

    private DetentionFacility() { }

    public static DetentionFacility Create(
        string name,
        string city,
        string state,
        FacilityType type,
        string? affNumber = null)
    {
        return new DetentionFacility
        {
            Name = name,
            City = city,
            State = state,
            Type = type,
            AffNumber = affNumber
        };
    }
}

public class DetentionPopulationSnapshot : BaseEntity
{
    public Guid FacilityId { get; private set; }
    public DetentionFacility Facility { get; private set; } = default!;
    public DateTime SnapshotDate { get; private set; }
    public int TotalDetained { get; private set; }
    public int? CriminalCount { get; private set; }
    public int? NonCriminalCount { get; private set; }
    public int? MaleCount { get; private set; }
    public int? FemaleCount { get; private set; }
    public int? AtdCount { get; private set; }
    public DataSource DataSource { get; private set; }

    private DetentionPopulationSnapshot() { }

    public static DetentionPopulationSnapshot Create(
        Guid facilityId,
        DateTime snapshotDate,
        int totalDetained,
        DataSource dataSource)
    {
        return new DetentionPopulationSnapshot
        {
            FacilityId = facilityId,
            SnapshotDate = snapshotDate,
            TotalDetained = totalDetained,
            DataSource = dataSource
        };
    }
}

public enum FacilityType
{
    ICEOwned,
    IGSA,
    CDF,
    SPC,
    BIA,
    Other
}
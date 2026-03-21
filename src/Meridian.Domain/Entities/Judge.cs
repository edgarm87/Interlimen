using Meridian.Domain.Common;

namespace Meridian.Domain.Entities;

public class Judge : BaseEntity
{
    /// <summary>
    /// The only way to create or modify a Judge is through the methods we explicitly define.
    /// This prevents the rest of the app from putting entities into invalid states.
    /// </summary>
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string FullName => $"{FirstName} {LastName}";
    public string CourtCity { get; private set; } = default!;
    public string CourtState { get; private set; } = default!;
    public string? CourtCode { get; private set; }
    public bool IsActive { get; private set; } = true;
    
    /// <summary>
    /// Denormalized stats (refreshed by background job)
    /// </summary>
    public int TotalCasesDecided { get; private set; }
    public decimal GrantRatePercent { get; private set; }
    public decimal DenialRatePercent { get; private set; }
    public decimal RemovalOrderRatePercent { get; private set; }
    public double? AverageDaysToDecision { get; private set; }
    public DateTime? StatsLastRefreshed { get; private set; }

    public ICollection<CourtCase> Cases { get; private set; } = new List<CourtCase>();

    /// <summary>
    /// This exists only for EF Core (it needs it to reconstruct objects from the database).
    /// The application code can never call new Judge() directly — it must go through Judge.Create(...)
    /// which enforces that required fields are always provided.
    /// </summary>
    private Judge() { }

    public static Judge Create(
        string firstName,
        string lastName,
        string courtCity,
        string courtState,
        string? courtCode = null)
    {
        return new Judge
        {
            FirstName = firstName,
            LastName = lastName,
            CourtCity = courtCity,
            CourtState = courtState,
            CourtCode = courtCode
        };
    }

    public void RefreshStats(
        int totalCases,
        decimal grantRate,
        decimal denialRate,
        decimal removalRate,
        double? avgDays)
    {
        TotalCasesDecided = totalCases;
        GrantRatePercent = grantRate;
        DenialRatePercent = denialRate;
        RemovalOrderRatePercent = removalRate;
        AverageDaysToDecision = avgDays;
        StatsLastRefreshed = DateTime.UtcNow;
        SetUpdated();
    }
}
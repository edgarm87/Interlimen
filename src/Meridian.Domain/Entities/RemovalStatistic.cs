using Meridian.Domain.Common;

namespace Meridian.Domain.Entities;

public class RemovalStatistic : BaseEntity
{
    public int Year { get; private set; }
    public int Month { get; private set; }
    public string Nationality { get; private set; } = default!;
    public string NationalityDisplay { get; private set; } = default!;

    public int TotalRemovals { get; private set; }
    public int CriminalRemovals { get; private set; }
    public int NonCriminalRemovals { get; private set; }
    public int? Returns { get; private set; }
    public int? InteriorRemovals { get; private set; }
    public int? BorderRemovals { get; private set; }

    public DataSource DataSource { get; private set; }

    private RemovalStatistic() { }

    public static RemovalStatistic Create(
        int year,
        int month,
        string nationality,
        string nationalityDisplay,
        int totalRemovals,
        int criminalRemovals,
        int nonCriminalRemovals,
        DataSource dataSource)
    {
        return new RemovalStatistic
        {
            Year = year,
            Month = month,
            Nationality = nationality,
            NationalityDisplay = nationalityDisplay,
            TotalRemovals = totalRemovals,
            CriminalRemovals = criminalRemovals,
            NonCriminalRemovals = nonCriminalRemovals,
            DataSource = dataSource
        };
    }
}

public class EnforcementAction : BaseEntity
{
    public string? SourceRecordId { get; private set; }
    public EnforcementActionType ActionType { get; private set; }
    public DateTime ActionDate { get; private set; }
    public string? Nationality { get; private set; }
    public string? CountryOfBirth { get; private set; }
    public string? AreaOfResponsibility { get; private set; }
    public string? ApprehensionMethod { get; private set; }
    public bool? HasCriminalConviction { get; private set; }
    public string? MostSeriousOffense { get; private set; }
    public string? BookingFacility { get; private set; }
    public DataSource DataSource { get; private set; }

    private EnforcementAction() { }

    public static EnforcementAction Create(
        EnforcementActionType type,
        DateTime actionDate,
        DataSource dataSource,
        string? sourceRecordId = null)
    {
        return new EnforcementAction
        {
            ActionType = type,
            ActionDate = actionDate,
            DataSource = dataSource,
            SourceRecordId = sourceRecordId
        };
    }
}

public enum EnforcementActionType
{
    Arrest,
    Detainer,
    Removal,
    VoluntaryReturn,
    Reinstatement
}
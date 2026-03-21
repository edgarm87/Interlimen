using Meridian.Domain.Common;

namespace Meridian.Domain.Entities;

public class CourtCase : BaseEntity
{
    public string CaseNumber { get; private set; } = default!;
    public string? RespondentNationality { get; private set; }
    public string? RespondentCustodyStatus { get; private set; }

    // Court & Judge
    public string CourtCity { get; private set; } = default!;
    public string CourtState { get; private set; } = default!;
    public Guid JudgeId { get; private set; }
    public Judge Judge { get; private set; } = default!;

    // Case Details
    public CaseType CaseType { get; private set; }
    public CaseStatus Status { get; private set; }
    public string? ChargesOfRemovability { get; private set; }
    public string? ReliefApplications { get; private set; }

    // Dates
    public DateTime? HearingDate { get; private set; }
    public DateTime? DecisionDate { get; private set; }
    public DateTime? FilingDate { get; private set; }

    // Outcomes
    public CaseOutcome? Outcome { get; private set; }
    public bool IsAppealed { get; private set; }
    public string? AppealOutcome { get; private set; }

    // Source tracking
    public DataSource DataSource { get; private set; }
    public string? SourceRecordId { get; private set; }

    private CourtCase() { }

    public static CourtCase Create(
        string caseNumber,
        string courtCity,
        string courtState,
        Guid judgeId,
        CaseType caseType,
        DataSource dataSource)
    {
        return new CourtCase
        {
            CaseNumber = caseNumber,
            CourtCity = courtCity,
            CourtState = courtState,
            JudgeId = judgeId,
            CaseType = caseType,
            Status = CaseStatus.Pending,
            DataSource = dataSource
        };
    }

    public void UpdateOutcome(CaseOutcome outcome, DateTime decisionDate)
    {
        Outcome = outcome;
        DecisionDate = decisionDate;
        Status = CaseStatus.Decided;
        SetUpdated();
    }

    public void SetHearing(DateTime hearingDate)
    {
        HearingDate = hearingDate;
        SetUpdated();
    }
}

/// <summary>
/// The enums below are tightly related to court cases and don't need their own files, keeps them easy to find.
/// </summary>
public enum CaseType
{
    Removal,
    Asylum,
    CancellationOfRemoval,
    VoluntaryDeparture,
    WithholdingOfRemoval,
    ProtectionUnderCAT,
    Other
}

public enum CaseStatus
{
    Pending,
    Scheduled,
    Continued,
    Decided,
    Appealed,
    Closed
}

public enum CaseOutcome
{
    GrantedRelief,
    DeniedRelief,
    OrderOfRemoval,
    VoluntaryDeparture,
    Terminated,
    Withdrawn,
    Other
}

public enum DataSource
{
    TRAC,
    OHSS,
    ICE_ERO,
    DeportationDataProject,
    CBP,
    Manual
}
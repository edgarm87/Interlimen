using Meridian.Domain.Common;

namespace Meridian.Domain.Entities;

/// <summary>
/// An ICE detainer is a request to a local jail/prison to hold someone for
/// transfer to ICE custody. Separate from arrest or detention — it's the
/// notification step in between.
/// Source: DDP 1dec2025 release (Sep 2023–Oct 2025)
/// </summary>
public class IceDetainer : BaseEntity
{
    // ── Core ──────────────────────────────────────────────────────────────────
    public DateTime DetainerPrepareDate { get; private set; }
    public string? FacilityState { get; private set; }
    public string? FacilityAor { get; private set; }
    public string? DetentionFacilityName { get; private set; }
    public string? DetentionFacilityCode { get; private set; }
    public string? FacilityCity { get; private set; }

    // ── Departure ─────────────────────────────────────────────────────────────
    public string? PortOfDeparture { get; private set; }
    public string? DepartureCountry { get; private set; }
    public DateTime? DepartedDate { get; private set; }

    // ── Case ──────────────────────────────────────────────────────────────────
    public string? CaseStatus { get; private set; }
    public string? CaseCategory { get; private set; }
    public string? DetainerCriminality { get; private set; }
    public string? ThreatLevel { get; private set; }
    public string? ProcessingDisposition { get; private set; }
    public string? FinalProgram { get; private set; }
    public bool? HasFinalOrder { get; private set; }
    public DateTime? FinalOrderDate { get; private set; }
    public DateTime? ApprehensionDate { get; private set; }

    // ── Demographics ──────────────────────────────────────────────────────────
    public string? CitizenshipCountry { get; private set; }
    public string? BirthCountry { get; private set; }
    public int? BirthYear { get; private set; }
    public string? Gender { get; private set; }
    public string? EntryStatus { get; private set; }

    // ── Criminal history flags ────────────────────────────────────────────────
    public bool? IsFelon { get; private set; }
    public bool? PriorFelony { get; private set; }
    public bool? AggravatedFelony { get; private set; }
    public bool? ViolentMisdemeanor { get; private set; }
    public string? MostSeriousConvictionCharge { get; private set; }

    // ── Detainer metadata ─────────────────────────────────────────────────────
    public string? DetainerType { get; private set; }
    public string? DetainerLiftReason { get; private set; }

    // ── Source tracking ───────────────────────────────────────────────────────
    public DataSource DataSource { get; private set; }
    public string? UniqueIdentifier { get; private set; }

    private IceDetainer() { }

    public static IceDetainer Create(
        DateTime detainerPrepareDate,
        DataSource dataSource,
        string? facilityState = null,
        string? facilityAor = null,
        string? detentionFacilityName = null,
        string? detentionFacilityCode = null,
        string? facilityCity = null,
        string? portOfDeparture = null,
        string? departureCountry = null,
        DateTime? departedDate = null,
        string? caseStatus = null,
        string? caseCategory = null,
        string? detainerCriminality = null,
        string? threatLevel = null,
        string? processingDisposition = null,
        string? finalProgram = null,
        bool? hasFinalOrder = null,
        DateTime? finalOrderDate = null,
        DateTime? apprehensionDate = null,
        string? citizenshipCountry = null,
        string? birthCountry = null,
        int? birthYear = null,
        string? gender = null,
        string? entryStatus = null,
        bool? isFelon = null,
        bool? priorFelony = null,
        bool? aggravatedFelony = null,
        bool? violentMisdemeanor = null,
        string? mostSeriousConvictionCharge = null,
        string? detainerType = null,
        string? detainerLiftReason = null,
        string? uniqueIdentifier = null)
    {
        return new IceDetainer
        {
            DetainerPrepareDate = detainerPrepareDate,
            DataSource = dataSource,
            FacilityState = facilityState,
            FacilityAor = facilityAor,
            DetentionFacilityName = detentionFacilityName,
            DetentionFacilityCode = detentionFacilityCode,
            FacilityCity = facilityCity,
            PortOfDeparture = portOfDeparture,
            DepartureCountry = departureCountry,
            DepartedDate = departedDate,
            CaseStatus = caseStatus,
            CaseCategory = caseCategory,
            DetainerCriminality = detainerCriminality,
            ThreatLevel = threatLevel,
            ProcessingDisposition = processingDisposition,
            FinalProgram = finalProgram,
            HasFinalOrder = hasFinalOrder,
            FinalOrderDate = finalOrderDate,
            ApprehensionDate = apprehensionDate,
            CitizenshipCountry = citizenshipCountry,
            BirthCountry = birthCountry,
            BirthYear = birthYear,
            Gender = gender,
            EntryStatus = entryStatus,
            IsFelon = isFelon,
            PriorFelony = priorFelony,
            AggravatedFelony = aggravatedFelony,
            ViolentMisdemeanor = violentMisdemeanor,
            MostSeriousConvictionCharge = mostSeriousConvictionCharge,
            DetainerType = detainerType,
            DetainerLiftReason = detainerLiftReason,
            UniqueIdentifier = uniqueIdentifier
        };
    }
}
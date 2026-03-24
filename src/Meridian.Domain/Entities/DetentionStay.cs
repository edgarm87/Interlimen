using Meridian.Domain.Common;

namespace Meridian.Domain.Entities;

/// <summary>
/// Represents a single stint in ICE detention at one facility.
/// One person may have multiple stints per stay if transferred between facilities.
/// Stay Book In/Out = the full detention period; Book In/Out = this facility only.
/// Source: DDP FOIA releases (ICFO_42034 FY2012-FY2023, 1dec2025 Sep2023-Oct2025)
/// </summary>
public class DetentionStay : BaseEntity
{
    // ── Facility ──────────────────────────────────────────────────────────────
    public Guid FacilityId { get; private set; }
    public DetentionFacility Facility { get; private set; } = default!;

    // ── Dates ─────────────────────────────────────────────────────────────────
    /// <summary>Start of the full detention stay (first facility in chain)</summary>
    public DateTime StayBookInDate { get; private set; }
    /// <summary>Book-in at this specific facility (may differ if transferred)</summary>
    public DateTime FacilityBookInDate { get; private set; }
    /// <summary>Book-out from this specific facility</summary>
    public DateTime? FacilityBookOutDate { get; private set; }
    /// <summary>End of full stay (book-out from last facility in chain)</summary>
    public DateTime? StayBookOutDate { get; private set; }

    // ── Release ───────────────────────────────────────────────────────────────
    public string? FacilityReleaseReason { get; private set; }
    public string? StayReleaseReason { get; private set; }

    // ── Case info ─────────────────────────────────────────────────────────────
    public string? CaseStatus { get; private set; }
    public string? CaseCategory { get; private set; }
    public bool? HasFinalOrder { get; private set; }
    public string? CaseThreatLevel { get; private set; }
    public string? BookInCriminality { get; private set; }

    // ── Demographics (available in newer releases only) ───────────────────────
    public string? CitizenshipCountry { get; private set; }
    public string? Gender { get; private set; }
    public int? BirthYear { get; private set; }
    public string? EntryStatus { get; private set; }

    // ── Bond ──────────────────────────────────────────────────────────────────
    public decimal? InitialBondAmount { get; private set; }
    public decimal? BondPostedAmount { get; private set; }

    // ── Source tracking ───────────────────────────────────────────────────────
    public DataSource DataSource { get; private set; }
    /// <summary>Anonymized cross-dataset linkage identifier from ICE</summary>
    public string? UniqueIdentifier { get; private set; }

    private DetentionStay() { }

    public static DetentionStay Create(
        Guid facilityId,
        DateTime stayBookInDate,
        DateTime facilityBookInDate,
        DataSource dataSource,
        DateTime? facilityBookOutDate = null,
        DateTime? stayBookOutDate = null,
        string? facilityReleaseReason = null,
        string? stayReleaseReason = null,
        string? caseStatus = null,
        string? caseCategory = null,
        bool? hasFinalOrder = null,
        string? caseThreatLevel = null,
        string? bookInCriminality = null,
        string? citizenshipCountry = null,
        string? gender = null,
        int? birthYear = null,
        string? entryStatus = null,
        decimal? initialBondAmount = null,
        decimal? bondPostedAmount = null,
        string? uniqueIdentifier = null)
    {
        return new DetentionStay
        {
            FacilityId = facilityId,
            StayBookInDate = stayBookInDate,
            FacilityBookInDate = facilityBookInDate,
            FacilityBookOutDate = facilityBookOutDate,
            StayBookOutDate = stayBookOutDate,
            FacilityReleaseReason = facilityReleaseReason,
            StayReleaseReason = stayReleaseReason,
            CaseStatus = caseStatus,
            CaseCategory = caseCategory,
            HasFinalOrder = hasFinalOrder,
            CaseThreatLevel = caseThreatLevel,
            BookInCriminality = bookInCriminality,
            CitizenshipCountry = citizenshipCountry,
            Gender = gender,
            BirthYear = birthYear,
            EntryStatus = entryStatus,
            InitialBondAmount = initialBondAmount,
            BondPostedAmount = bondPostedAmount,
            UniqueIdentifier = uniqueIdentifier,
            DataSource = dataSource
        };
    }
}
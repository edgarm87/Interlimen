using Meridian.Domain.Entities;

namespace Meridian.Domain.Interfaces;

public interface IJudgeRepository : IRepository<Judge>
{
    Task<IReadOnlyList<Judge>> GetByCourtAsync(string courtCity, string courtState, CancellationToken ct = default);
    Task<IReadOnlyList<Judge>> GetWithStatsAsync(CancellationToken ct = default);
    Task<Judge?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Judge?> GetByNameAndCourtAsync(string firstName, string lastName, string courtCity, CancellationToken ct = default);
}

public interface ICourtCaseRepository : IRepository<CourtCase>
{
    Task<IReadOnlyList<CourtCase>> GetByJudgeAsync(Guid judgeId, int? year = null, CancellationToken ct = default, int? fromYear = null, int? toYear = null, string? sortBy = null);
    Task<IReadOnlyList<CourtCase>> GetByCourtAsync(string courtCity, string courtState, CancellationToken ct = default);
    Task<CourtBacklogSummary> GetBacklogSummaryAsync(string? courtCity = null, string? courtState = null, CancellationToken ct = default);
    Task<IReadOnlyList<CourtCase>> GetPendingByNationalityAsync(string nationality, CancellationToken ct = default);
    Task<(int total, int pending, int decided)> GetCountsByJudgeAsync(Guid judgeId, CancellationToken ct = default);
    Task<IReadOnlyList<CourtCase>> GetByCourtFilteredAsync(string courtCity, string? courtState = null, string? nationality = null, CaseOutcome? outcome = null, CancellationToken ct = default);
    Task<CourtCase?> GetByCaseNumberAsync(string caseNumber, CancellationToken ct = default);
}

public interface IDetentionFacilityRepository : IRepository<DetentionFacility>
{
    Task<IReadOnlyList<DetentionFacility>> GetActiveWithLatestPopulationAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DetentionPopulationSnapshot>> GetPopulationTrendAsync(Guid facilityId, int months = 12, CancellationToken ct = default);
    Task<int> GetTotalCurrentDetaineesAsync(CancellationToken ct = default);
    Task<List<(DateTime Date, int Total)>> GetSystemPopulationTrendAsync(int months = 24, CancellationToken ct = default);
}

public interface IRemovalStatisticRepository : IRepository<RemovalStatistic>
{
    Task<IReadOnlyList<RemovalStatistic>> GetByNationalityAsync(string nationality, int? year = null, CancellationToken ct = default);
    Task<IReadOnlyList<RemovalStatistic>> GetMonthlyTrendAsync(int months = 24, CancellationToken ct = default);
    Task<IReadOnlyList<(string Nationality, int Total)>> GetTopNationalitiesAsync(int topN = 10, int? year = null, CancellationToken ct = default);
}

public interface IAlertRuleRepository : IRepository<AlertRule>
{
    Task<IReadOnlyList<AlertRule>> GetActiveByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<AlertNotification>> GetUnreadNotificationsAsync(string userId, CancellationToken ct = default);
}

public interface ITenantRepository : IRepository<Tenant>
{
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Tenant?> GetByStripeCustomerIdAsync(string stripeCustomerId, CancellationToken ct = default);
}

public interface IEnforcementActionRepository : IRepository<EnforcementAction>
{
    Task<List<(string AOR, int Total, int Criminal)>> GetByAORAsync(int? year, CancellationToken ct = default);
    Task<List<(string AOR, int Total, int Criminal)>> GetArrestsByAORAsync(int? year, CancellationToken ct = default); 
    Task<List<(string Code, int Count)>> GetByDispositionAsync(int? year, CancellationToken ct = default);
    Task<List<(string Nationality, int Count)>> GetTopNationalitiesAsync(int topN, int? year, CancellationToken ct = default);
    Task<(int Total, int Criminal, string TopNationality, string TopAOR, DateTime Earliest, DateTime Latest)> GetSummaryAsync(int? year, CancellationToken ct = default);
    Task<int> GetCountByTypeAsync(EnforcementActionType type, CancellationToken ct = default);
    Task<List<(string Nationality, int Count)>> GetTopNationalitiesByAORAsync(string aor, int topN, CancellationToken ct = default);
    Task<List<(int Year, int Month, int Total)>> GetMonthlyTrendByAORAsync(string aor, CancellationToken ct = default);
    Task<(DateTime Earliest, DateTime Latest)> GetDateRangeByTypeAsync(EnforcementActionType type, CancellationToken ct = default);
}

public interface IIceDetainerRepository : IRepository<IceDetainer>
{
    Task<int> GetActiveCountAsync(CancellationToken ct = default);
    Task<(int Total, int Active, int Removed, string TopState)> GetSummaryAsync(CancellationToken ct = default);
    Task<(List<(Guid Id, DateTime PrepareDate, string? State, string? Aor, string? FacilityName, string? Country, string? Criminality, string? CaseStatus, DateTime? DepartedDate, string? DepartureCountry)> Items, int Total)> GetPagedAsync(int page, int pageSize, string? state, string? country, string? criminality, string? facilityName, bool? activeOnly, CancellationToken ct = default);
}

public record CourtBacklogSummary(
    string CourtCity,
    string CourtState,
    int TotalPending,
    int ScheduledForNextMonth,
    double AverageWaitDays,
    DateTime AsOf
);

public interface IDetentionStayRepository : IRepository<DetentionStay>
{
    Task<(int Total, int Active, double AvgDays, string TopReason)> GetSummaryAsync(CancellationToken ct = default);
    Task<(List<(Guid Id, Guid FacilityId, string? FacilityName, string? Nationality, string? Gender, int? BirthYear, string? Criminality, string? CaseStatus, string? StayReleaseReason, string? FacilityReleaseReason, DateTime BookInDate, DateTime? BookOutDate)> Items, int Total)> GetPagedAsync(
        int page, int pageSize, string? facilityName, string? nationality,
        string? releaseReason, bool? activeOnly, int? fromYear, int? toYear,
        CancellationToken ct = default);
}
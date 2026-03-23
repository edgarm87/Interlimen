using Meridian.Domain.Entities;

namespace Meridian.Domain.Interfaces;

public interface IJudgeRepository : IRepository<Judge>
{
    Task<IReadOnlyList<Judge>> GetByCourtAsync(string courtCity, string courtState, CancellationToken ct = default);
    Task<IReadOnlyList<Judge>> GetWithStatsAsync(CancellationToken ct = default);
    Task<Judge?> GetByNameAndCourtAsync(string firstName, string lastName, string courtCity, CancellationToken ct = default);
}

public interface ICourtCaseRepository : IRepository<CourtCase>
{
    Task<IReadOnlyList<CourtCase>> GetByJudgeAsync(Guid judgeId, int? year = null, CancellationToken ct = default);
    Task<IReadOnlyList<CourtCase>> GetByCourtAsync(string courtCity, string courtState, CancellationToken ct = default);
    Task<CourtBacklogSummary> GetBacklogSummaryAsync(string? courtCity = null, string? courtState = null, CancellationToken ct = default);
    Task<IReadOnlyList<CourtCase>> GetPendingByNationalityAsync(string nationality, CancellationToken ct = default);
    Task<(int total, int pending, int decided)> GetCountsByJudgeAsync(Guid judgeId, CancellationToken ct = default);
}

public interface IDetentionFacilityRepository : IRepository<DetentionFacility>
{
    Task<IReadOnlyList<DetentionFacility>> GetActiveWithLatestPopulationAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DetentionPopulationSnapshot>> GetPopulationTrendAsync(Guid facilityId, int months = 12, CancellationToken ct = default);
    Task<int> GetTotalCurrentDetaineesAsync(CancellationToken ct = default);
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
    Task<List<(string Code, int Count)>> GetByDispositionAsync(int? year, CancellationToken ct = default);
    Task<List<(string Nationality, int Count)>> GetTopNationalitiesAsync(int topN, int? year, CancellationToken ct = default);
    Task<(int Total, int Criminal, string TopNationality, string TopAOR, DateTime Earliest, DateTime Latest)> GetSummaryAsync(int? year, CancellationToken ct = default);
}

public record CourtBacklogSummary(
    string CourtCity,
    string CourtState,
    int TotalPending,
    int ScheduledForNextMonth,
    double AverageWaitDays,
    DateTime AsOf
);
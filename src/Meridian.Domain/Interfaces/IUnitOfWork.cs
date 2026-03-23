using Meridian.Domain.Entities;

namespace Meridian.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IJudgeRepository Judges { get; }
    ICourtCaseRepository CourtCases { get; }
    IDetentionFacilityRepository DetentionFacilities { get; }
    IRemovalStatisticRepository RemovalStatistics { get; }
    IAlertRuleRepository AlertRules { get; }
    ITenantRepository Tenants { get; }
    IEnforcementActionRepository EnforcementActions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
using Meridian.Domain.Interfaces;
using Meridian.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace Meridian.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;

    public IJudgeRepository Judges { get; }
    public ICourtCaseRepository CourtCases { get; }
    public IDetentionFacilityRepository DetentionFacilities { get; }
    public IRemovalStatisticRepository RemovalStatistics { get; }
    public IAlertRuleRepository AlertRules { get; }
    public ITenantRepository Tenants { get; }

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
        Judges = new JudgeRepository(context);
        CourtCases = new CourtCaseRepository(context);
        DetentionFacilities = new DetentionFacilityRepository(context);
        RemovalStatistics = new RemovalStatisticRepository(context);
        AlertRules = new AlertRuleRepository(context);
        Tenants = new TenantRepository(context);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default)
        => _transaction = await _context.Database.BeginTransactionAsync(ct);

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
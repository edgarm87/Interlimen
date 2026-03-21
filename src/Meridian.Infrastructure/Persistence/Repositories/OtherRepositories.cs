using Meridian.Domain.Entities;
using Meridian.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Infrastructure.Persistence.Repositories;

public class DetentionFacilityRepository : Repository<DetentionFacility>, IDetentionFacilityRepository
{
    public DetentionFacilityRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<DetentionFacility>> GetActiveWithLatestPopulationAsync(
        CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Include(f => f.PopulationSnapshots
                .OrderByDescending(s => s.SnapshotDate)
                .Take(1))
            .Where(f => f.IsActive)
            .OrderBy(f => f.State)
            .ThenBy(f => f.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DetentionPopulationSnapshot>> GetPopulationTrendAsync(
        Guid facilityId,
        int months = 12,
        CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-months);
        return await _context.DetentionSnapshots
            .AsNoTracking()
            .Where(s => s.FacilityId == facilityId && s.SnapshotDate >= cutoff)
            .OrderBy(s => s.SnapshotDate)
            .ToListAsync(ct);
    }

    public async Task<int> GetTotalCurrentDetaineesAsync(CancellationToken ct = default)
    {
        var snapshots = await _context.DetentionSnapshots
            .AsNoTracking()
            .GroupBy(s => s.FacilityId)
            .Select(g => g.OrderByDescending(s => s.SnapshotDate).First())
            .ToListAsync(ct);

        return snapshots.Sum(s => s.TotalDetained);
    }
}

public class RemovalStatisticRepository : Repository<RemovalStatistic>, IRemovalStatisticRepository
{
    public RemovalStatisticRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<RemovalStatistic>> GetByNationalityAsync(
        string nationality,
        int? year = null,
        CancellationToken ct = default)
    {
        var query = _dbSet.AsNoTracking().Where(s => s.Nationality == nationality);
        if (year.HasValue) query = query.Where(s => s.Year == year.Value);
        return await query
            .OrderByDescending(s => s.Year)
            .ThenByDescending(s => s.Month)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RemovalStatistic>> GetMonthlyTrendAsync(
        int months = 24,
        CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-months);
        var cutoffYear = cutoff.Year;
        var cutoffMonth = cutoff.Month;

        return await _dbSet
            .AsNoTracking()
            .Where(s => s.Year > cutoffYear ||
                       (s.Year == cutoffYear && s.Month >= cutoffMonth))
            .OrderBy(s => s.Year)
            .ThenBy(s => s.Month)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<(string Nationality, int Total)>> GetTopNationalitiesAsync(
        int topN = 10,
        int? year = null,
        CancellationToken ct = default)
    {
        var query = _dbSet.AsNoTracking();
        if (year.HasValue) query = query.Where(s => s.Year == year.Value);

        var result = await query
            .GroupBy(s => s.NationalityDisplay)
            .Select(g => new
            {
                Nationality = g.Key,
                Total = g.Sum(s => s.TotalRemovals)
            })
            .OrderByDescending(x => x.Total)
            .Take(topN)
            .ToListAsync(ct);

        return result.Select(x => (x.Nationality, x.Total)).ToList();
    }
}

public class AlertRuleRepository : Repository<AlertRule>, IAlertRuleRepository
{
    public AlertRuleRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<AlertRule>> GetActiveByTenantAsync(
        Guid tenantId,
        CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.IsActive)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AlertNotification>> GetUnreadNotificationsAsync(
        string userId,
        CancellationToken ct = default)
        => await _context.AlertNotifications
            .AsNoTracking()
            .Include(n => n.AlertRule)
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);
}

public class TenantRepository : Repository<Tenant>, ITenantRepository
{
    public TenantRepository(AppDbContext context) : base(context) { }

    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(t => t.Slug == slug, ct);

    public async Task<Tenant?> GetByStripeCustomerIdAsync(
        string stripeCustomerId,
        CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(t => t.StripeCustomerId == stripeCustomerId, ct);
}
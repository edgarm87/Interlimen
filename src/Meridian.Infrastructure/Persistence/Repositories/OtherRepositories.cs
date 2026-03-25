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
    
    public async Task<List<(DateTime Date, int Total)>> GetSystemPopulationTrendAsync(
        int months = 24,
        CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-months);

        var results = await _context.DetentionSnapshots
            .AsNoTracking()
            .Where(s => s.SnapshotDate >= cutoff)
            .GroupBy(s => s.SnapshotDate.Date)
            .Select(g => new
            {
                Date = g.Key,
                Total = g.Sum(s => s.TotalDetained)
            })
            .OrderBy(x => x.Date)
            .ToListAsync(ct);

        return results.Select(x => (x.Date, x.Total)).ToList();
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
        // For annual data (Month=0), just return all records
        // Monthly filtering will apply when we have real monthly data
        return await _dbSet
            .AsNoTracking()
            .OrderBy(s => s.Year)
            .ThenBy(s => s.Month)
            .ToListAsync(ct);
        
        // var cutoff = DateTime.UtcNow.AddMonths(-months);
        // var cutoffYear = cutoff.Year;
        // var cutoffMonth = cutoff.Month;
        //
        // return await _dbSet
        //     .AsNoTracking()
        //     .Where(s => s.Year > cutoffYear ||
        //                (s.Year == cutoffYear && s.Month >= cutoffMonth))
        //     .OrderBy(s => s.Year)
        //     .ThenBy(s => s.Month)
        //     .ToListAsync(ct);
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

public class EnforcementActionRepository
    : Repository<EnforcementAction>, IEnforcementActionRepository
{
    private readonly AppDbContext _db;

    public EnforcementActionRepository(AppDbContext context) : base(context)
        => _db = context;

    public async Task<List<(string AOR, int Total, int Criminal)>> GetByAORAsync(
        int? year, CancellationToken ct = default)
    {
        var results = await _db.EnforcementActions
            .Where(e => e.ActionType == EnforcementActionType.Removal
                        && e.AreaOfResponsibility != null
                        && (year == null || e.ActionDate.Year == year))
            .GroupBy(e => e.AreaOfResponsibility)
            .Select(g => new
            {
                AOR = g.Key!,
                Total = g.Count(),
                Criminal = 0  // not derivable from DDP removals data
            })
            .OrderByDescending(x => x.Total)
            .ToListAsync(ct);

        return results.Select(x => (x.AOR, x.Total, x.Criminal)).ToList();
    }

    public async Task<List<(string Code, int Count)>> GetByDispositionAsync(
        int? year, CancellationToken ct = default)
    {
        var results = await _db.EnforcementActions
            .Where(e => e.ActionType == EnforcementActionType.Removal
                        && e.DispositionCode != null
                        && (year == null || e.ActionDate.Year == year))
            .GroupBy(e => e.DispositionCode)
            .Select(g => new { Code = g.Key!, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(ct);

        return results.Select(x => (x.Code, x.Count)).ToList();
    }

    public async Task<List<(string Nationality, int Count)>> GetTopNationalitiesAsync(
        int topN, int? year, CancellationToken ct = default)
    {
        var results = await _db.EnforcementActions
            .Where(e => e.ActionType == EnforcementActionType.Removal
                        && e.Nationality != null
                        && (year == null || e.ActionDate.Year == year))
            .GroupBy(e => e.Nationality)
            .Select(g => new { Nationality = g.Key!, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(topN)
            .ToListAsync(ct);

        return results.Select(x => (x.Nationality, x.Count)).ToList();
    }

    public async Task<(int Total, int Criminal, string TopNationality, string TopAOR, DateTime Earliest, DateTime Latest)>
        GetSummaryAsync(int? year, CancellationToken ct = default)
    {
        var baseQuery = _db.EnforcementActions
            .Where(e => e.ActionType == EnforcementActionType.Removal
                && (year == null || e.ActionDate.Year == year));

        var total = await baseQuery.CountAsync(ct);
        var criminal = await baseQuery.CountAsync(e => e.HasCriminalConviction == true, ct);
        var earliest = await baseQuery.MinAsync(e => e.ActionDate, ct);
        var latest = await baseQuery.MaxAsync(e => e.ActionDate, ct);

        var topNationality = await baseQuery
            .Where(e => e.Nationality != null)
            .GroupBy(e => e.Nationality)
            .Select(g => new { g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Select(x => x.Key)
            .FirstOrDefaultAsync(ct) ?? "N/A";

        var topAOR = await _db.EnforcementActions
            .Where(e => e.ActionType == EnforcementActionType.Arrest
                        && e.AreaOfResponsibility != null)
            .GroupBy(e => e.AreaOfResponsibility)
            .Select(g => new { g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Select(x => x.Key)
            .FirstOrDefaultAsync(ct) ?? "N/A";

        return (total, criminal, topNationality, topAOR, earliest, latest);
    }
    
    public async Task<List<(string AOR, int Total, int Criminal)>> GetArrestsByAORAsync(
        int? year, CancellationToken ct = default)
    {
        var results = await _db.EnforcementActions
            .Where(e => e.ActionType == EnforcementActionType.Arrest
                        && e.AreaOfResponsibility != null
                        && (year == null || e.ActionDate.Year == year))
            .GroupBy(e => e.AreaOfResponsibility)
            .Select(g => new
            {
                AOR = g.Key!,
                Total = g.Count(),
                Criminal = g.Count(e => e.HasCriminalConviction == true)
            })
            .OrderByDescending(x => x.Total)
            .ToListAsync(ct);

        return results.Select(x => (x.AOR, x.Total, x.Criminal)).ToList();
    }
    
    public async Task<int> GetCountByTypeAsync(
        EnforcementActionType type, CancellationToken ct = default)
        => await _db.EnforcementActions
            .CountAsync(e => e.ActionType == type, ct);
}

public class IceDetainerRepository : Repository<IceDetainer>, IIceDetainerRepository
{
    public IceDetainerRepository(AppDbContext context) : base(context) { }

    public async Task<int> GetActiveCountAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-6);
        return await _context.IceDetainers
            .CountAsync(d => d.DepartedDate == null
                             && d.DetainerPrepareDate >= cutoff, ct);
    }
}
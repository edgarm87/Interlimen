using Meridian.Domain.Entities;
using Meridian.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

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

    public async Task<List<(string Nationality, int Count)>> GetTopNationalitiesByAORAsync(
        string aor, int topN, CancellationToken ct = default)
    {
        var results = await _db.EnforcementActions
            .Where(e => e.ActionType == EnforcementActionType.Arrest
                        && e.AreaOfResponsibility == aor + " Area of Responsibility"
                        && e.Nationality != null)
            .GroupBy(e => e.Nationality)
            .Select(g => new { Nationality = g.Key!, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(topN)
            .ToListAsync(ct);

        return results.Select(x => (x.Nationality, x.Count)).ToList();
    }

    public async Task<List<(int Year, int Month, int Total)>> GetMonthlyTrendByAORAsync(
        string aor, CancellationToken ct = default)
    {
        var results = await _db.EnforcementActions
            .Where(e => e.ActionType == EnforcementActionType.Arrest
                        && e.AreaOfResponsibility == aor + " Area of Responsibility")
            .GroupBy(e => new { e.ActionDate.Year, e.ActionDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);

        return results.Select(x => (x.Year, x.Month, x.Total)).ToList();
    }
    
    public async Task<(DateTime Earliest, DateTime Latest)> GetDateRangeByTypeAsync(
        EnforcementActionType type, CancellationToken ct = default)
    {
        var earliest = await _db.EnforcementActions
            .Where(e => e.ActionType == type)
            .MinAsync(e => e.ActionDate, ct);
        var latest = await _db.EnforcementActions
            .Where(e => e.ActionType == type)
            .MaxAsync(e => e.ActionDate, ct);
        return (earliest, latest);
    }
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

    public async Task<(int Total, int Active, int Removed, string TopState)> GetSummaryAsync(
        CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-6);

        var total = await _context.IceDetainers.CountAsync(ct);
        var active = await _context.IceDetainers
            .CountAsync(d => d.DepartedDate == null
                             && d.DetainerPrepareDate >= cutoff, ct);
        var removed = await _context.IceDetainers
            .CountAsync(d => d.DepartedDate != null, ct);

        var topState = await _context.IceDetainers
            .Where(d => d.FacilityState != null)
            .GroupBy(d => d.FacilityState)
            .Select(g => new { State = g.Key!, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Select(x => x.State)
            .FirstOrDefaultAsync(ct) ?? "N/A";

        return (total, active, removed, topState);
    }

    public async Task<(List<(Guid Id, DateTime PrepareDate, string? State, string? Aor, string? FacilityName, string? Country, string? Criminality, string? CaseStatus, DateTime? DepartedDate, string? DepartureCountry)> Items, int Total)> GetPagedAsync(
        int page, int pageSize, string? state, string? country, string? criminality,
        string? facilityName, bool? activeOnly, CancellationToken ct = default,
        string sortBy = "PrepareDate", bool sortDescending = true)
    {
        var query = _context.IceDetainers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(state))
            query = query.Where(d => d.FacilityState == state);

        if (!string.IsNullOrWhiteSpace(country))
            query = query.Where(d => d.CitizenshipCountry == country);

        if (!string.IsNullOrWhiteSpace(criminality))
            query = query.Where(d => d.DetainerCriminality == criminality);

        if (!string.IsNullOrWhiteSpace(facilityName))
            query = query.Where(d => d.DetentionFacilityName != null &&
                                     d.DetentionFacilityName.Contains(facilityName));

        if (activeOnly == true)
            query = query.Where(d => d.DepartedDate == null);

        var total = await query.CountAsync(ct);

        IOrderedQueryable<IceDetainer> Order<T>(System.Linq.Expressions.Expression<Func<IceDetainer, T>> key)
            => sortDescending ? query.OrderByDescending(key) : query.OrderBy(key);

        var ordered = sortBy switch
        {
            "State" => Order(d => d.FacilityState),
            "Facility" => Order(d => d.DetentionFacilityName),
            "Nationality" => Order(d => d.CitizenshipCountry),
            "Criminality" => Order(d => d.DetainerCriminality),
            // Active precedes Resolved in ascending order, matching the displayed labels.
            "Status" => Order(d => d.DepartedDate != null),
            "Departed" => Order(d => d.DepartedDate),
            "Destination" => Order(d => d.DepartureCountry),
            _ => Order(d => d.DetainerPrepareDate)
        };

        var items = await ordered
            .ThenBy(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                d.Id,
                d.DetainerPrepareDate,
                d.FacilityState,
                d.FacilityAor,
                d.DetentionFacilityName,
                d.CitizenshipCountry,
                d.DetainerCriminality,
                d.CaseStatus,
                d.DepartedDate,
                d.DepartureCountry
            })
            .ToListAsync(ct);

        var result = items.Select(d => (
            d.Id,
            d.DetainerPrepareDate,
            d.FacilityState,
            d.FacilityAor,
            d.DetentionFacilityName,
            d.CitizenshipCountry,
            d.DetainerCriminality,
            d.CaseStatus,
            d.DepartedDate,
            d.DepartureCountry
        )).ToList();

        return (result, total);
    }
}

public class DetentionStayRepository : Repository<DetentionStay>, IDetentionStayRepository
{
    public DetentionStayRepository(AppDbContext context) : base(context) { }

    public async Task<(int Total, int Active, double AvgDays, string TopReason)> GetSummaryAsync(
        CancellationToken ct = default)
    {
        var total = await _context.DetentionStays.CountAsync(ct);

        var cutoff = new DateTime(2022, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var active = await _context.DetentionStays
            .CountAsync(s => s.StayBookOutDate == null && s.StayBookInDate >= cutoff, ct);

        var conn = (Npgsql.NpgsqlConnection)_context.Database.GetDbConnection();
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT AVG(EXTRACT(EPOCH FROM (""StayBookOutDate"" - ""StayBookInDate"")) / 86400) FROM detention_stays WHERE ""StayBookOutDate"" IS NOT NULL";
        var result = await cmd.ExecuteScalarAsync(ct);
        var avgDays = result is DBNull || result == null ? 0.0 : Math.Round(Convert.ToDouble(result), 1);
        await conn.CloseAsync();

        var topReason = await _context.DetentionStays
            .Where(s => s.StayReleaseReason != null)
            .GroupBy(s => s.StayReleaseReason)
            .Select(g => new { Reason = g.Key!, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Select(x => x.Reason)
            .FirstOrDefaultAsync(ct) ?? "N/A";

        return (total, active, Math.Round(avgDays, 1), topReason);
    }

    public async Task<(List<(Guid Id, Guid FacilityId, string? FacilityName, string? Nationality, string? Gender, int? BirthYear, string? Criminality, string? CaseStatus, string? StayReleaseReason, string? FacilityReleaseReason, DateTime BookInDate, DateTime? BookOutDate)> Items, int Total)> GetPagedAsync(
        int page, int pageSize, string? facilityName, string? nationality,
        string? releaseReason, bool? activeOnly, int? fromYear, int? toYear,
        CancellationToken ct = default)
    {
        var query = _context.DetentionStays
            .AsNoTracking()
            .Join(_context.DetentionFacilities,
                s => s.FacilityId,
                f => f.Id,
                (s, f) => new { Stay = s, Facility = f });

        if (!string.IsNullOrWhiteSpace(facilityName))
            query = query.Where(x => x.Facility.Name.Contains(facilityName));

        if (!string.IsNullOrWhiteSpace(nationality))
            query = query.Where(x => x.Stay.CitizenshipCountry == nationality);

        if (!string.IsNullOrWhiteSpace(releaseReason))
            query = query.Where(x => x.Stay.StayReleaseReason == releaseReason);

        if (activeOnly == true)
            query = query.Where(x => x.Stay.StayBookOutDate == null);

        if (fromYear.HasValue)
            query = query.Where(x => x.Stay.StayBookInDate.Year >= fromYear.Value);

        if (toYear.HasValue)
            query = query.Where(x => x.Stay.StayBookInDate.Year <= toYear.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.Stay.StayBookInDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Stay.Id,
                x.Stay.FacilityId,
                FacilityName = x.Facility.Name,
                x.Stay.CitizenshipCountry,
                x.Stay.Gender,
                x.Stay.BirthYear,
                x.Stay.BookInCriminality,
                x.Stay.CaseStatus,
                x.Stay.StayReleaseReason,
                x.Stay.FacilityReleaseReason,
                x.Stay.StayBookInDate,
                x.Stay.StayBookOutDate
            })
            .ToListAsync(ct);

        var result = items.Select(x => (
            x.Id,
            x.FacilityId,
            (string?)x.FacilityName,
            x.CitizenshipCountry,
            x.Gender,
            x.BirthYear,
            x.BookInCriminality,
            x.CaseStatus,
            x.StayReleaseReason,
            x.FacilityReleaseReason,
            x.StayBookInDate,
            x.StayBookOutDate
        )).ToList();

        return (result, total);
    }
}
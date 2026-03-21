using Meridian.Domain.Entities;
using Meridian.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Infrastructure.Persistence.Repositories;

public class JudgeRepository : Repository<Judge>, IJudgeRepository
{
    public JudgeRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Judge>> GetByCourtAsync(
        string courtCity,
        string courtState,
        CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Where(j => j.CourtCity == courtCity && j.CourtState == courtState && j.IsActive)
            .OrderBy(j => j.LastName)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Judge>> GetWithStatsAsync(CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Where(j => j.IsActive && j.TotalCasesDecided > 0)
            .OrderByDescending(j => j.TotalCasesDecided)
            .ToListAsync(ct);

    public async Task<Judge?> GetByNameAndCourtAsync(
        string firstName,
        string lastName,
        string courtCity,
        CancellationToken ct = default)
        => await _dbSet
            .FirstOrDefaultAsync(j =>
                j.FirstName == firstName &&
                j.LastName == lastName &&
                j.CourtCity == courtCity, ct);
}

public class CourtCaseRepository : Repository<CourtCase>, ICourtCaseRepository
{
    public CourtCaseRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<CourtCase>> GetByJudgeAsync(
        Guid judgeId,
        int? year = null,
        CancellationToken ct = default)
    {
        var query = _dbSet.AsNoTracking().Where(c => c.JudgeId == judgeId);
        if (year.HasValue)
            query = query.Where(c => c.FilingDate.HasValue && c.FilingDate.Value.Year == year.Value);
        return await query.OrderByDescending(c => c.FilingDate).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CourtCase>> GetByCourtAsync(
        string courtCity,
        string courtState,
        CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Where(c => c.CourtCity == courtCity && c.CourtState == courtState)
            .OrderByDescending(c => c.FilingDate)
            .ToListAsync(ct);

    public async Task<CourtBacklogSummary> GetBacklogSummaryAsync(
        string? courtCity = null,
        string? courtState = null,
        CancellationToken ct = default)
    {
        var query = _dbSet.AsNoTracking()
            .Where(c => c.Status == CaseStatus.Pending ||
                        c.Status == CaseStatus.Scheduled ||
                        c.Status == CaseStatus.Continued);

        if (!string.IsNullOrEmpty(courtCity))
            query = query.Where(c => c.CourtCity == courtCity);
        if (!string.IsNullOrEmpty(courtState))
            query = query.Where(c => c.CourtState == courtState);

        var cases = await query.ToListAsync(ct);
        var now = DateTime.UtcNow;
        var nextMonth = now.AddMonths(1);

        var totalPending = cases.Count;
        var scheduledNext = cases.Count(c =>
            c.HearingDate.HasValue && c.HearingDate.Value <= nextMonth);
        var avgWait = cases
            .Where(c => c.FilingDate.HasValue)
            .Select(c => (now - c.FilingDate!.Value).TotalDays)
            .DefaultIfEmpty(0)
            .Average();

        return new CourtBacklogSummary(
            courtCity ?? "All Courts",
            courtState ?? "All States",
            totalPending,
            scheduledNext,
            Math.Round(avgWait, 1),
            now
        );
    }

    public async Task<IReadOnlyList<CourtCase>> GetPendingByNationalityAsync(
        string nationality,
        CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .Where(c => c.RespondentNationality == nationality &&
                       (c.Status == CaseStatus.Pending ||
                        c.Status == CaseStatus.Scheduled))
            .ToListAsync(ct);

    public async Task<(int total, int pending, int decided)> GetCountsByJudgeAsync(
        Guid judgeId,
        CancellationToken ct = default)
    {
        var counts = await _dbSet
            .AsNoTracking()
            .Where(c => c.JudgeId == judgeId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Pending = g.Count(c =>
                    c.Status == CaseStatus.Pending ||
                    c.Status == CaseStatus.Scheduled),
                Decided = g.Count(c => c.Status == CaseStatus.Decided)
            })
            .FirstOrDefaultAsync(ct);

        return counts is null ? (0, 0, 0) : (counts.Total, counts.Pending, counts.Decided);
    }
}
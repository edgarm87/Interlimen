using Meridian.Domain.Entities;
using Meridian.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Infrastructure.Persistence.Repositories;

public class JudgeRepository : Repository<Judge>, IJudgeRepository
{
    public JudgeRepository(AppDbContext context) : base(context) { }

    public async Task<Judge?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbSet.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, ct);
    
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
        CancellationToken ct = default,
        int? fromYear = null,
        int? toYear = null,
        string? sortBy = null)
    {
        var query = _dbSet.AsNoTracking().Where(c => c.JudgeId == judgeId);

        if (year.HasValue)
            query = query.Where(c => c.DecisionDate.HasValue && c.DecisionDate.Value.Year == year.Value);

        if (fromYear.HasValue)
            query = query.Where(c => c.DecisionDate.HasValue && c.DecisionDate.Value.Year >= fromYear.Value);

        if (toYear.HasValue)
            query = query.Where(c => c.DecisionDate.HasValue && c.DecisionDate.Value.Year <= toYear.Value);

        query = sortBy switch
        {
            "date_asc" => query.OrderBy(c => c.DecisionDate),
            "outcome" => query.OrderBy(c => c.Outcome),
            "nationality" => query.OrderBy(c => c.RespondentNationality),
            _ => query.OrderByDescending(c => c.DecisionDate)
        };

        return await query.Take(200).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CourtCase>> GetByCourtAsync(
        string courtCity,
        string courtState,
        CancellationToken ct = default)
        => await GetByCourtFilteredAsync(courtCity, courtState, null, null, ct);

    public async Task<IReadOnlyList<CourtCase>> GetByCourtFilteredAsync(
        string courtCity,
        string? courtState = null,
        string? nationality = null,
        CaseOutcome? outcome = null,
        CancellationToken ct = default)
    {
        var query = _dbSet.AsNoTracking()
            .Where(c => c.CourtCity == courtCity);

        if (!string.IsNullOrWhiteSpace(courtState))
            query = query.Where(c => c.CourtState == courtState);

        if (!string.IsNullOrWhiteSpace(nationality))
            query = query.Where(c => c.RespondentNationality == nationality);

        if (outcome.HasValue)
            query = query.Where(c => c.Outcome == outcome.Value);

        return await query
            .OrderByDescending(c => c.DecisionDate)
            .Take(200)
            .ToListAsync(ct);
    }
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

        var totalPending = await query.CountAsync(ct);

        var now = DateTime.UtcNow;
        var nextMonth = now.AddMonths(1);

        var scheduledNext = await query
            .CountAsync(c => c.HearingDate.HasValue && c.HearingDate.Value <= nextMonth, ct);

        var avgWaitResult = await query
            .Where(c => c.FilingDate.HasValue)
            .Select(c => (now - c.FilingDate!.Value).TotalDays)
            .DefaultIfEmpty(0)
            .AverageAsync(ct);

        var avgWait = Math.Round(avgWaitResult, 1);

        return new CourtBacklogSummary(
            courtCity ?? "All Courts",
            courtState ?? "All States",
            totalPending,
            scheduledNext,
            avgWait,
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
    
    public async Task<CourtCase?> GetByCaseNumberAsync(
        string caseNumber, CancellationToken ct = default)
        => await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CaseNumber == caseNumber, ct);
}
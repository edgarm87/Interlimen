using Meridian.Domain.Interfaces;
using MediatR;

namespace Meridian.Application.Features.Removals;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record RemovalByNationalityDto(
    string Nationality,
    string NationalityDisplay,
    int Year,
    int Month,
    int TotalRemovals,
    int CriminalRemovals,
    int NonCriminalRemovals
);

public record NationalityRankingDto(
    string Nationality,
    string NationalityDisplay,
    int TotalRemovals,
    decimal PercentOfTotal
);

public record YearlyRemovalTotalDto(
    int Year,
    int TotalRemovals,
    int CriminalRemovals,
    int NonCriminalRemovals
);

// ── Queries ───────────────────────────────────────────────────────────────────

public record GetRemovalsByNationality(
    string Nationality,
    int? Year = null) : IRequest<List<RemovalByNationalityDto>>;

public class GetRemovalsByNationalityHandler
    : IRequestHandler<GetRemovalsByNationality, List<RemovalByNationalityDto>>
{
    private readonly IRemovalStatisticRepository _stats;

    public GetRemovalsByNationalityHandler(IRemovalStatisticRepository stats)
        => _stats = stats;

    public async Task<List<RemovalByNationalityDto>> Handle(
        GetRemovalsByNationality request,
        CancellationToken ct)
    {
        var stats = await _stats.GetByNationalityAsync(
            request.Nationality,
            request.Year,
            ct);

        return stats.Select(s => new RemovalByNationalityDto(
            s.Nationality,
            s.NationalityDisplay,
            s.Year,
            s.Month,
            s.TotalRemovals,
            s.CriminalRemovals,
            s.NonCriminalRemovals
        )).ToList();
    }
}

public record GetTopNationalities(
    int TopN = 10,
    int? Year = null) : IRequest<List<NationalityRankingDto>>;

public class GetTopNationalitiesHandler
    : IRequestHandler<GetTopNationalities, List<NationalityRankingDto>>
{
    private readonly IRemovalStatisticRepository _stats;

    public GetTopNationalitiesHandler(IRemovalStatisticRepository stats)
        => _stats = stats;

    public async Task<List<NationalityRankingDto>> Handle(
        GetTopNationalities request,
        CancellationToken ct)
    {
        var top = await _stats.GetTopNationalitiesAsync(request.TopN, request.Year, ct);
        var total = top.Sum(x => x.Total);

        return top.Select(x => new NationalityRankingDto(
            x.Nationality,
            x.Nationality,
            x.Total,
            total > 0 ? Math.Round((decimal)x.Total / total * 100, 2) : 0
        )).ToList();
    }
}

public record GetYearlyRemovalTotals() : IRequest<List<YearlyRemovalTotalDto>>;

public class GetYearlyRemovalTotalsHandler
    : IRequestHandler<GetYearlyRemovalTotals, List<YearlyRemovalTotalDto>>
{
    private readonly IRemovalStatisticRepository _stats;

    public GetYearlyRemovalTotalsHandler(IRemovalStatisticRepository stats)
        => _stats = stats;

    public async Task<List<YearlyRemovalTotalDto>> Handle(
        GetYearlyRemovalTotals request,
        CancellationToken ct)
    {
        var all = await _stats.GetMonthlyTrendAsync(120, ct);

        return all
            .GroupBy(s => s.Year)
            .Select(g => new YearlyRemovalTotalDto(
                g.Key,
                g.Sum(s => s.TotalRemovals),
                g.Sum(s => s.CriminalRemovals),
                g.Sum(s => s.NonCriminalRemovals)
            ))
            .OrderByDescending(x => x.Year)
            .ToList();
    }
}

public record GetAllRemovalsByYear(int Year) : IRequest<List<RemovalByNationalityDto>>;

public class GetAllRemovalsByYearHandler
    : IRequestHandler<GetAllRemovalsByYear, List<RemovalByNationalityDto>>
{
    private readonly IRemovalStatisticRepository _stats;

    public GetAllRemovalsByYearHandler(IRemovalStatisticRepository stats)
        => _stats = stats;

    public async Task<List<RemovalByNationalityDto>> Handle(
        GetAllRemovalsByYear request,
        CancellationToken ct)
    {
        var all = await _stats.FindAsync(
            s => s.Year == request.Year && s.Month == 0, ct);

        return all
            .OrderByDescending(s => s.TotalRemovals)
            .Select(s => new RemovalByNationalityDto(
                s.Nationality,
                s.NationalityDisplay,
                s.Year,
                s.Month,
                s.TotalRemovals,
                s.CriminalRemovals,
                s.NonCriminalRemovals
            )).ToList();
    }
}
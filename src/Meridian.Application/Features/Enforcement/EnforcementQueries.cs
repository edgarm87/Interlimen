using Meridian.Domain.Entities;
using Meridian.Domain.Interfaces;
using MediatR;

namespace Meridian.Application.Features.Enforcement;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record EnforcementSummaryDto(
    int TotalRemovals,
    int CriminalArrests,
    int NonCriminalRemovals,
    string TopNationality,
    string TopAOR,
    DateTime EarliestDate,
    DateTime LatestDate
);

public record RemovalsByAORDto(
    string AreaOfResponsibility,
    int TotalRemovals,
    int CriminalRemovals,
    decimal CriminalPercent
);

public record RemovalsByDispositionDto(
    string DispositionCode,
    string DispositionLabel,
    int Count,
    decimal Percent
);

public record RemovalsByNationalityICEDto(
    string Nationality,
    int Count,
    decimal Percent
);

public record ArrestsByFieldOfficeDto(
    string FieldOffice,
    int TotalArrests,
    int CriminalArrests,
    decimal CriminalPercent
);

// ── Queries ───────────────────────────────────────────────────────────────────

public record GetRemovalsByAOR(int? Year = null) : IRequest<List<RemovalsByAORDto>>;
public record GetRemovalsByDisposition(int? Year = null) : IRequest<List<RemovalsByDispositionDto>>;
public record GetTopNationalitiesICE(int TopN = 10, int? Year = null) : IRequest<List<RemovalsByNationalityICEDto>>;
public record GetEnforcementSummary(int? Year = null) : IRequest<EnforcementSummaryDto>;
public record GetArrestsByFieldOffice(int? Year = null) : IRequest<List<ArrestsByFieldOfficeDto>>;
public record AorDetailDto(
    string FieldOffice,
    int TotalArrests,
    int CriminalArrests,
    decimal CriminalPercent,
    int NonCriminalArrests,
    decimal NonCriminalPercent,
    List<(string Nationality, int Count)> TopNationalities,
    List<(int Year, int Month, int Total)> MonthlyTrend,
    decimal NationalAvgMonthly
);

public record GetAorDetail(string Aor) : IRequest<AorDetailDto>;
public record GetActiveDetainersCount() : IRequest<int>;
public record GetActionCount(EnforcementActionType ActionType) : IRequest<int>;

// ── Handlers ──────────────────────────────────────────────────────────────────

public class GetRemovalsByAORHandler : IRequestHandler<GetRemovalsByAOR, List<RemovalsByAORDto>>
{
    private readonly IEnforcementActionRepository _repo;
    public GetRemovalsByAORHandler(IEnforcementActionRepository repo) => _repo = repo;

    public async Task<List<RemovalsByAORDto>> Handle(GetRemovalsByAOR request, CancellationToken ct)
    {
        var results = await _repo.GetByAORAsync(request.Year, ct);
        return results.Select(r => new RemovalsByAORDto(
            r.AOR.Replace(" Area of Responsibility", ""),
            r.Total,
            r.Criminal,
            r.Total > 0 ? Math.Round((decimal)r.Criminal / r.Total * 100, 1) : 0
        )).OrderByDescending(x => x.TotalRemovals).ToList();
    }
}

public class GetRemovalsByDispositionHandler
    : IRequestHandler<GetRemovalsByDisposition, List<RemovalsByDispositionDto>>
{
    private readonly IEnforcementActionRepository _repo;
    public GetRemovalsByDispositionHandler(IEnforcementActionRepository repo) => _repo = repo;

    public async Task<List<RemovalsByDispositionDto>> Handle(
        GetRemovalsByDisposition request, CancellationToken ct)
    {
        var results = await _repo.GetByDispositionAsync(request.Year, ct);
        var total = results.Sum(r => r.Count);

        return results.Select(r => new RemovalsByDispositionDto(
            r.Code,
            r.Code,
            r.Count,
            total > 0 ? Math.Round((decimal)r.Count / total * 100, 1) : 0
        )).ToList();
    }
}

public class GetTopNationalitiesICEHandler
    : IRequestHandler<GetTopNationalitiesICE, List<RemovalsByNationalityICEDto>>
{
    private readonly IEnforcementActionRepository _repo;
    public GetTopNationalitiesICEHandler(IEnforcementActionRepository repo) => _repo = repo;

    public async Task<List<RemovalsByNationalityICEDto>> Handle(
        GetTopNationalitiesICE request, CancellationToken ct)
    {
        var results = await _repo.GetTopNationalitiesAsync(request.TopN, request.Year, ct);
        var total = results.Sum(r => r.Count);

        return results.Select(r => new RemovalsByNationalityICEDto(
            r.Nationality,
            r.Count,
            total > 0 ? Math.Round((decimal)r.Count / total * 100, 1) : 0
        )).ToList();
    }
}

public class GetEnforcementSummaryHandler
    : IRequestHandler<GetEnforcementSummary, EnforcementSummaryDto>
{
    private readonly IEnforcementActionRepository _repo;
    public GetEnforcementSummaryHandler(IEnforcementActionRepository repo) => _repo = repo;

    public async Task<EnforcementSummaryDto> Handle(
        GetEnforcementSummary request, CancellationToken ct)
    {
        var summary = await _repo.GetSummaryAsync(request.Year, ct);
        return new EnforcementSummaryDto(
            summary.Total,
            summary.Criminal,
            summary.Total - summary.Criminal,
            summary.TopNationality,
            summary.TopAOR.Replace(" Area of Responsibility", ""),
            summary.Earliest,
            summary.Latest
        );
    }
}

public class GetArrestsByFieldOfficeHandler
    : IRequestHandler<GetArrestsByFieldOffice, List<ArrestsByFieldOfficeDto>>
{
    private readonly IEnforcementActionRepository _repo;
    public GetArrestsByFieldOfficeHandler(IEnforcementActionRepository repo) => _repo = repo;

    public async Task<List<ArrestsByFieldOfficeDto>> Handle(
        GetArrestsByFieldOffice request, CancellationToken ct)
    {
        var results = await _repo.GetArrestsByAORAsync(request.Year, ct);
        return results.Select(r => new ArrestsByFieldOfficeDto(
            r.AOR.Replace(" Area of Responsibility", ""),
            r.Total,
            r.Criminal,
            r.Total > 0 ? Math.Round((decimal)r.Criminal / r.Total * 100, 1) : 0
        )).OrderByDescending(x => x.TotalArrests).ToList();
    }
}

public class GetAorDetailHandler : IRequestHandler<GetAorDetail, AorDetailDto>
{
    private readonly IEnforcementActionRepository _repo;
    public GetAorDetailHandler(IEnforcementActionRepository repo) => _repo = repo;

    public async Task<AorDetailDto> Handle(GetAorDetail request, CancellationToken ct)
    {
        var allAors = await _repo.GetArrestsByAORAsync(null, ct);
        var aor = allAors.FirstOrDefault(x =>
            x.AOR.Replace(" Area of Responsibility", "") == request.Aor);

        var total = aor.Total;
        var criminal = aor.Criminal;
        var nonCriminal = total - criminal;

        var topNats = await _repo.GetTopNationalitiesByAORAsync(request.Aor, 5, ct);
        var trend = await _repo.GetMonthlyTrendByAORAsync(request.Aor, ct);

        var nationalTotal = allAors.Sum(x => x.Total);
        var nationalAvgMonthly = trend.Count > 0
            ? Math.Round((decimal)nationalTotal / allAors.Count / Math.Max(trend.Count, 1), 0)
            : 0;

        return new AorDetailDto(
            request.Aor,
            total,
            criminal,
            total > 0 ? Math.Round((decimal)criminal / total * 100, 1) : 0,
            nonCriminal,
            total > 0 ? Math.Round((decimal)nonCriminal / total * 100, 1) : 0,
            topNats,
            trend,
            nationalAvgMonthly
        );
    }
}

public record GetActionDateRange(EnforcementActionType ActionType) : IRequest<(DateTime Earliest, DateTime Latest)>;

public class GetActionDateRangeHandler : IRequestHandler<GetActionDateRange, (DateTime Earliest, DateTime Latest)>
{
    private readonly IEnforcementActionRepository _repo;
    public GetActionDateRangeHandler(IEnforcementActionRepository repo) => _repo = repo;

    public async Task<(DateTime Earliest, DateTime Latest)> Handle(GetActionDateRange request, CancellationToken ct)
        => await _repo.GetDateRangeByTypeAsync(request.ActionType, ct);
}

public class GetActiveDetainersCountHandler
    : IRequestHandler<GetActiveDetainersCount, int>
{
    private readonly IIceDetainerRepository _repo;
    public GetActiveDetainersCountHandler(IIceDetainerRepository repo) => _repo = repo;

    public async Task<int> Handle(GetActiveDetainersCount request, CancellationToken ct)
        => await _repo.GetActiveCountAsync(ct);
}

public class GetActionCountHandler : IRequestHandler<GetActionCount, int>
{
    private readonly IEnforcementActionRepository _repo;
    public GetActionCountHandler(IEnforcementActionRepository repo) => _repo = repo;

    public async Task<int> Handle(GetActionCount request, CancellationToken ct)
        => await _repo.GetCountByTypeAsync(request.ActionType, ct);
}
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
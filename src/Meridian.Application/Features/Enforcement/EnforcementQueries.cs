using Meridian.Domain.Common;
using Meridian.Domain.Entities;
using Meridian.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Meridian.Application.Features.Enforcement;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record EnforcementSummaryDto(
    int TotalRemovals,
    int CriminalRemovals,
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

// ── Queries ───────────────────────────────────────────────────────────────────

public record GetRemovalsByAOR(int? Year = null) : IRequest<List<RemovalsByAORDto>>;

public record GetRemovalsByDisposition(int? Year = null) : IRequest<List<RemovalsByDispositionDto>>;

public record GetTopNationalitiesICE(int TopN = 10, int? Year = null) : IRequest<List<RemovalsByNationalityICEDto>>;

public record GetEnforcementSummary(int? Year = null) : IRequest<EnforcementSummaryDto>;

// ── Handlers ──────────────────────────────────────────────────────────────────

public class GetRemovalsByAORHandler : IRequestHandler<GetRemovalsByAOR, List<RemovalsByAORDto>>
{
    private readonly IRepository<EnforcementAction> _repo;

    public GetRemovalsByAORHandler(IRepository<EnforcementAction> repo) => _repo = repo;

    public async Task<List<RemovalsByAORDto>> Handle(GetRemovalsByAOR request, CancellationToken ct)
    {
        var all = await _repo.FindAsync(e =>
            e.ActionType == EnforcementActionType.Removal &&
            (request.Year == null || e.ActionDate.Year == request.Year), ct);

        return all
            .Where(e => !string.IsNullOrEmpty(e.AreaOfResponsibility))
            .GroupBy(e => e.AreaOfResponsibility!)
            .Select(g =>
            {
                var total = g.Count();
                var criminal = g.Count(e => e.HasCriminalConviction == true);
                return new RemovalsByAORDto(
                    g.Key.Replace(" Area of Responsibility", ""),
                    total,
                    criminal,
                    total > 0 ? Math.Round((decimal)criminal / total * 100, 1) : 0
                );
            })
            .OrderByDescending(x => x.TotalRemovals)
            .ToList();
    }
}

public class GetRemovalsByDispositionHandler
    : IRequestHandler<GetRemovalsByDisposition, List<RemovalsByDispositionDto>>
{
    private readonly IRepository<EnforcementAction> _repo;

    public GetRemovalsByDispositionHandler(IRepository<EnforcementAction> repo) => _repo = repo;

    public async Task<List<RemovalsByDispositionDto>> Handle(
        GetRemovalsByDisposition request, CancellationToken ct)
    {
        var all = await _repo.FindAsync(e =>
            e.ActionType == EnforcementActionType.Removal &&
            (request.Year == null || e.ActionDate.Year == request.Year), ct);

        var labels = new Dictionary<string, string>
        {
            { "REINST", "Reinstatement of Removal" },
            { "ER", "Expedited Removal" },
            { "V", "Voluntary Departure" },
            { "T", "Transfer" },
            { "NTA", "Notice to Appear" },
            { "WA/NTA", "Warrant/Notice to Appear" },
            { "ADMDPT", "Administrative Deportation" },
            { "B", "Bond" },
            { "NTA/DT", "NTA Detention" },
            { "ER/CF", "Expedited Removal/Criminal Foreign" }
        };

        var total = all.Count;
        return all
            .Where(e => !string.IsNullOrEmpty(e.ApprehensionMethod))
            .GroupBy(e => e.ApprehensionMethod!)
            .Select(g => new RemovalsByDispositionDto(
                g.Key,
                labels.GetValueOrDefault(g.Key, g.Key),
                g.Count(),
                total > 0 ? Math.Round((decimal)g.Count() / total * 100, 1) : 0
            ))
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();
    }
}

public class GetTopNationalitiesICEHandler
    : IRequestHandler<GetTopNationalitiesICE, List<RemovalsByNationalityICEDto>>
{
    private readonly IRepository<EnforcementAction> _repo;

    public GetTopNationalitiesICEHandler(IRepository<EnforcementAction> repo) => _repo = repo;

    public async Task<List<RemovalsByNationalityICEDto>> Handle(
        GetTopNationalitiesICE request, CancellationToken ct)
    {
        var all = await _repo.FindAsync(e =>
            e.ActionType == EnforcementActionType.Removal &&
            (request.Year == null || e.ActionDate.Year == request.Year), ct);

        var total = all.Count;
        return all
            .Where(e => !string.IsNullOrEmpty(e.Nationality))
            .GroupBy(e => e.Nationality!)
            .Select(g => new RemovalsByNationalityICEDto(
                g.Key,
                g.Count(),
                total > 0 ? Math.Round((decimal)g.Count() / total * 100, 1) : 0
            ))
            .OrderByDescending(x => x.Count)
            .Take(request.TopN)
            .ToList();
    }
}

public class GetEnforcementSummaryHandler
    : IRequestHandler<GetEnforcementSummary, EnforcementSummaryDto>
{
    private readonly IRepository<EnforcementAction> _repo;

    public GetEnforcementSummaryHandler(IRepository<EnforcementAction> repo) => _repo = repo;

    public async Task<EnforcementSummaryDto> Handle(
        GetEnforcementSummary request, CancellationToken ct)
    {
        var all = await _repo.FindAsync(e =>
            e.ActionType == EnforcementActionType.Removal &&
            (request.Year == null || e.ActionDate.Year == request.Year), ct);

        var topNationality = all
            .Where(e => !string.IsNullOrEmpty(e.Nationality))
            .GroupBy(e => e.Nationality!)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key ?? "N/A";

        var topAOR = all
            .Where(e => !string.IsNullOrEmpty(e.AreaOfResponsibility))
            .GroupBy(e => e.AreaOfResponsibility!)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key?.Replace(" Area of Responsibility", "") ?? "N/A";

        return new EnforcementSummaryDto(
            all.Count,
            all.Count(e => e.HasCriminalConviction == true),
            all.Count(e => e.HasCriminalConviction == false),
            topNationality,
            topAOR,
            all.Min(e => e.ActionDate),
            all.Max(e => e.ActionDate)
        );
    }
}
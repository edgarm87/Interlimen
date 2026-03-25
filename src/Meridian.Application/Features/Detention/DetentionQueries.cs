using Meridian.Domain.Interfaces;
using MediatR;

namespace Meridian.Application.Features.Detention;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record FacilityPopulationDto(
    Guid FacilityId,
    string Name,
    string City,
    string State,
    string FacilityType,
    int? RatedCapacity,
    int CurrentDetained,
    decimal? OccupancyPercent,
    DateTime SnapshotDate
);

public record PopulationTrendPointDto(
    DateTime Date,
    int TotalDetained,
    int? Criminal,
    int? NonCriminal
);

// ── Queries ───────────────────────────────────────────────────────────────────

public record GetActiveFacilitiesWithPopulation() : IRequest<List<FacilityPopulationDto>>;

public class GetActiveFacilitiesHandler
    : IRequestHandler<GetActiveFacilitiesWithPopulation, List<FacilityPopulationDto>>
{
    private readonly IDetentionFacilityRepository _facilities;

    public GetActiveFacilitiesHandler(IDetentionFacilityRepository facilities)
        => _facilities = facilities;

    public async Task<List<FacilityPopulationDto>> Handle(
        GetActiveFacilitiesWithPopulation request,
        CancellationToken ct)
    {
        var facilities = await _facilities.GetActiveWithLatestPopulationAsync(ct);

        return facilities.Select(f =>
        {
            var latest = f.PopulationSnapshots
                .OrderByDescending(s => s.SnapshotDate)
                .FirstOrDefault();
            var current = latest?.TotalDetained ?? 0;
            decimal? occupancy = f.RatedCapacity.HasValue && f.RatedCapacity > 0
                ? Math.Round((decimal)current / f.RatedCapacity.Value * 100, 1)
                : null;

            return new FacilityPopulationDto(
                f.Id,
                f.Name,
                f.City,
                f.State,
                f.Type.ToString(),
                f.RatedCapacity,
                current,
                occupancy,
                latest?.SnapshotDate ?? DateTime.UtcNow
            );
        }).ToList();
    }
}

public record GetFacilityTrend(Guid FacilityId, int Months = 12)
    : IRequest<List<PopulationTrendPointDto>>;

public class GetFacilityTrendHandler
    : IRequestHandler<GetFacilityTrend, List<PopulationTrendPointDto>>
{
    private readonly IDetentionFacilityRepository _facilities;

    public GetFacilityTrendHandler(IDetentionFacilityRepository facilities)
        => _facilities = facilities;

    public async Task<List<PopulationTrendPointDto>> Handle(
        GetFacilityTrend request,
        CancellationToken ct)
    {
        var snapshots = await _facilities.GetPopulationTrendAsync(
            request.FacilityId,
            request.Months,
            ct);

        return snapshots.Select(s => new PopulationTrendPointDto(
            s.SnapshotDate,
            s.TotalDetained,
            s.CriminalCount,
            s.NonCriminalCount
        )).ToList();
    }
}

public record GetSystemPopulationTrend(int Months = 24)
    : IRequest<List<PopulationTrendPointDto>>;

public class GetSystemPopulationTrendHandler
    : IRequestHandler<GetSystemPopulationTrend, List<PopulationTrendPointDto>>
{
    private readonly IDetentionFacilityRepository _facilities;

    public GetSystemPopulationTrendHandler(IDetentionFacilityRepository facilities)
        => _facilities = facilities;

    public async Task<List<PopulationTrendPointDto>> Handle(
        GetSystemPopulationTrend request,
        CancellationToken ct)
    {
        var results = await _facilities.GetSystemPopulationTrendAsync(request.Months, ct);
        return results
            .Select(x => new PopulationTrendPointDto(x.Date, x.Total, null, null))
            .ToList();
    }
}